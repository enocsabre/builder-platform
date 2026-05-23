using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuilderPlatform.Infrastructure.Services;

// ── Domain records ──────────────────────────────────────────────────────────

public record OperationalBottleneck(
    string Title,
    string Description,
    string Severity,      // "critical" | "high" | "medium"
    string ImpactArea,
    string Resolution,
    string Risk,
    int    ImpactScore    // 0-100
);

public record WorkflowStatus(
    string       Name,
    bool         IsCritical,
    string       Phase,          // "operational" | "partial" | "missing"
    int          Coverage,       // 0-100
    List<string> PresentSteps,
    List<string> MissingSteps,
    string       BusinessImpact
);

public record ImpactSuggestion(
    string Title,
    string OperationalValue,
    string ImpactLevel,   // "high" | "medium" | "low"
    string Urgency        // "immediate" | "near-term" | "later"
);

public record OperationalReport(
    string                     ProductId,
    string                     Industry,
    string                     IndustryLabel,
    int                        OperationalScore,
    string                     OperationalTier,   // "broken"|"limited"|"functional"|"optimized"
    string                     OperationalTierLabel,
    string                     OperationalNarrative,
    string                     TopBottleneckTitle,
    string                     TopBottleneckResolution,
    List<OperationalBottleneck> Bottlenecks,
    List<WorkflowStatus>       Workflows,
    List<ImpactSuggestion>     TopImpactSuggestions,
    DateTime                   AnalyzedAt
);

// ── Engine ──────────────────────────────────────────────────────────────────

public class ProductOperationalImpactEngine
{
    public async Task<OperationalReport> AnalyzeAsync(Guid productId, AppDbContext db, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Modules)
            .Include(p => p.Memory)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null) return Empty(productId);

        var industryKey  = product.Memory.FirstOrDefault(m => m.Key == "industry")?.Value ?? "general";
        var moduleNames  = product.Modules
            .Where(m => m.IsActive)
            .Select(m => m.ModuleName.ToLowerInvariant())
            .ToList();

        var profile = GetProfile(industryKey);

        // ── Workflow evaluation ──────────────────────────────────────────────
        var workflows = profile.Workflows.Select(wf =>
        {
            var present = wf.Steps.Where(s => Has(s.Keywords, moduleNames)).Select(s => s.Name).ToList();
            var missing = wf.Steps.Where(s => !Has(s.Keywords, moduleNames)).Select(s => s.Name).ToList();
            var cov     = wf.Steps.Length == 0 ? 0 : present.Count * 100 / wf.Steps.Length;
            var phase   = cov == 100 ? "operational" : cov > 0 ? "partial" : "missing";
            return new WorkflowStatus(wf.Name, wf.IsCritical, phase, cov, present, missing, wf.BusinessImpact);
        }).ToList();

        // ── Bottleneck detection ─────────────────────────────────────────────
        var bottlenecks = new List<OperationalBottleneck>();
        foreach (var rule in profile.BottleneckRules)
        {
            if (Has(rule.TriggerKeywords, moduleNames) && !Has(rule.MissingKeywords, moduleNames))
                bottlenecks.Add(new OperationalBottleneck(
                    rule.Title, rule.Description, rule.Severity,
                    rule.ImpactArea, rule.Resolution, rule.Risk, rule.ImpactScore));
        }
        bottlenecks = bottlenecks.OrderByDescending(b => b.ImpactScore).ToList();

        // ── Operational score ────────────────────────────────────────────────
        var totalSteps   = profile.Workflows.Sum(w => w.Steps.Length);
        var coveredSteps = workflows.Sum(w => w.PresentSteps.Count);
        var coverage     = totalSteps == 0 ? 0 : coveredSteps * 100 / totalSteps;

        var penalty  = Math.Min(55, bottlenecks.Count(b => b.Severity == "critical") * 18
                                  + bottlenecks.Count(b => b.Severity == "high")     *  9
                                  + bottlenecks.Count(b => b.Severity == "medium")   *  3);
        var score    = Math.Max(0, Math.Min(100, coverage - penalty));

        var tier      = score switch { < 20 => "broken", < 50 => "limited", < 75 => "functional", _ => "optimized" };
        var tierLabel = tier switch { "broken" => "Crítico", "limited" => "Limitado", "functional" => "Funcional", _ => "Optimizado" };

        // ── Top impact suggestions ───────────────────────────────────────────
        var suggestions = BuildSuggestions(bottlenecks, workflows, moduleNames.Count);

        var topB      = bottlenecks.FirstOrDefault();
        var narrative = BuildNarrative(industryKey, tier, score, bottlenecks.Count, moduleNames.Count);

        return new OperationalReport(
            productId.ToString(), industryKey, IndustryLabel(industryKey),
            score, tier, tierLabel, narrative,
            topB?.Title       ?? "Sin cuellos de botella detectados",
            topB?.Resolution  ?? "Agregar módulos core para habilitar el análisis operacional.",
            bottlenecks, workflows, suggestions,
            DateTime.UtcNow
        );
    }

    // ── Static helpers ───────────────────────────────────────────────────────

    private static bool Has(string[] keywords, List<string> moduleNames) =>
        keywords.Any(k => moduleNames.Any(m => m.Contains(k)));

    private static string IndustryLabel(string k) => k switch
    {
        "restaurant"  => "Restaurante",
        "hr_payroll"  => "RRHH y Planilla",
        "veterinary"  => "Veterinaria",
        "ecommerce"   => "E-Commerce",
        "logistics"   => "Logística",
        "real_estate" => "Bienes Raíces",
        "healthcare"  => "Salud",
        "education"   => "Educación",
        _             => "General",
    };

    private static string BuildNarrative(string industry, string tier, int score, int bottleneckCount, int moduleCount)
    {
        var label = industry switch
        {
            "restaurant" => "restaurante",
            "hr_payroll" => "sistema de RRHH",
            "veterinary" => "clínica veterinaria",
            "ecommerce"  => "tienda en línea",
            "logistics"  => "operación logística",
            _            => "sistema",
        };

        return (tier, moduleCount, bottleneckCount) switch
        {
            ("broken", 0, _)   => $"El {label} no tiene módulos activos. Sin digitalización, cada operación es manual y propensa a errores.",
            ("broken", _, _)   => $"El {label} tiene módulos activos pero {bottleneckCount} cuello{(bottleneckCount == 1 ? "" : "s")} de botella crítico{(bottleneckCount == 1 ? "" : "s")} que impiden una operación fluida. Prioridad: resolver bloqueos antes de agregar funcionalidades.",
            ("limited", _, 0)  => $"El {label} tiene cobertura parcial ({score}%). Los workflows críticos están incompletos — hay ineficiencias operacionales que generan trabajo manual diario.",
            ("limited", _, _)  => $"El {label} tiene cobertura parcial ({score}%) y {bottleneckCount} cuello{(bottleneckCount == 1 ? "" : "s")} de botella. Resolver los bloqueos detectados tendría impacto inmediato en la operación.",
            ("functional", _, 0) => $"El {label} opera de forma funcional ({score}%). Los workflows críticos están cubiertos — el siguiente paso es integrar módulos de crecimiento y analytics.",
            ("functional", _, _) => $"El {label} tiene buena cobertura ({score}%) pero {bottleneckCount} gap{(bottleneckCount == 1 ? "" : "s")} operacional{(bottleneckCount == 1 ? "" : "es")} que reducen la eficiencia. Resolver los cuellos de botella haría la operación significativamente más fluida.",
            _                   => $"El {label} tiene una operación sólida ({score}%). La mayoría de los workflows críticos están cubiertos y bien integrados.",
        };
    }

    private static List<ImpactSuggestion> BuildSuggestions(
        List<OperationalBottleneck> bottlenecks,
        List<WorkflowStatus> workflows,
        int moduleCount)
    {
        var suggestions = new List<ImpactSuggestion>();

        // From critical bottlenecks
        foreach (var b in bottlenecks.Where(b => b.Severity == "critical").Take(2))
            suggestions.Add(new ImpactSuggestion(b.Resolution, b.Risk, "high", "immediate"));

        // From high bottlenecks
        foreach (var b in bottlenecks.Where(b => b.Severity == "high").Take(2))
            suggestions.Add(new ImpactSuggestion(b.Resolution, b.Risk, "high", "immediate"));

        // From partial workflows missing one step
        foreach (var wf in workflows.Where(w => w.Phase == "partial" && w.MissingSteps.Count == 1 && w.IsCritical).Take(2))
            suggestions.Add(new ImpactSuggestion(
                $"Completar workflow: {wf.Name}",
                $"El workflow está al {wf.Coverage}% — un paso faltante ({wf.MissingSteps[0]}) lo completa. {wf.BusinessImpact}",
                "medium", "near-term"));

        // From missing workflows
        foreach (var wf in workflows.Where(w => w.Phase == "missing" && w.IsCritical).Take(1))
            suggestions.Add(new ImpactSuggestion(
                $"Iniciar workflow: {wf.Name}",
                wf.BusinessImpact,
                "high", moduleCount == 0 ? "immediate" : "near-term"));

        return suggestions.Take(4).ToList();
    }

    private static OperationalReport Empty(Guid productId) =>
        new(productId.ToString(), "general", "General",
            0, "broken", "Crítico",
            "No hay datos de producto para analizar.",
            "Sin cuellos de botella detectados",
            "Registrar el producto para habilitar el análisis operacional.",
            [], [], [], DateTime.UtcNow);

    // ── Profile types ────────────────────────────────────────────────────────

    private sealed record WorkflowStep(string Name, string[] Keywords);
    private sealed record WorkflowDef(string Name, bool IsCritical, string BusinessImpact, WorkflowStep[] Steps);
    private sealed record BottleneckRule(
        string[] TriggerKeywords, string[] MissingKeywords,
        string Severity, string Title, string Description,
        string ImpactArea, string Resolution, string Risk,
        int ImpactScore);
    private sealed record IndustryProfile(List<WorkflowDef> Workflows, List<BottleneckRule> BottleneckRules);

    private static IndustryProfile GetProfile(string key) => key switch
    {
        "restaurant"  => RestaurantProfile(),
        "hr_payroll"  => HrPayrollProfile(),
        "veterinary"  => VeterinaryProfile(),
        "ecommerce"   => EcommerceProfile(),
        "logistics"   => LogisticsProfile(),
        "real_estate" => RealEstateProfile(),
        "healthcare"  => HealthcareProfile(),
        "education"   => EducationProfile(),
        _             => GeneralProfile(),
    };

    // ── Restaurant ───────────────────────────────────────────────────────────

    private static IndustryProfile RestaurantProfile() => new(
        Workflows:
        [
            new("Flujo de Ventas", true,
                "Sin este flujo completo, cada pedido genera fricción — errores, retrasos y clientes insatisfechos.",
                [
                    new("Menú Digital",      ["menu", "dish", "carta", "plato", "product"]),
                    new("Gestión de Pedidos",["order", "pedido"]),
                    new("Cocina / KDS",      ["kitchen", "kds", "cocina"]),
                    new("Mesas",             ["table", "mesa"]),
                ]),

            new("Flujo de Inventario", true,
                "Sin control de inventario, el restaurante compra en pánico, pierde insumos y no sabe su costo real.",
                [
                    new("Control de Stock",  ["inventor", "inventario", "stock"]),
                    new("Proveedores",       ["supplier", "proveedor"]),
                    new("Órdenes de Compra", ["purchase", "compra"]),
                ]),

            new("Flujo de Personal", false,
                "Sin control de personal, los costos laborales son impredecibles y los turnos se gestionan informalmente.",
                [
                    new("Personal / Turnos", ["staff", "empleado", "personal", "waiter", "mesero"]),
                    new("Planilla",          ["payroll", "planilla", "salario"]),
                ]),
        ],

        BottleneckRules:
        [
            new(["order", "pedido"], ["kitchen", "kds", "cocina"],
                "critical", "Pedidos sin comunicación a cocina",
                "Los pedidos existen en el sistema pero la cocina no los recibe digitalmente — los meseros gritan o llevan papel. En horas pico esto genera errores y retrasos inevitables.",
                "Operaciones de cocina", "Implementar módulo de Cocina / KDS",
                "Errores frecuentes en pedidos, tiempos de entrega inconsistentes, clientes insatisfechos.",
                92),

            new(["table", "mesa"], ["order", "pedido"],
                "critical", "Mesas sin sistema de pedidos",
                "Las mesas están registradas pero no hay sistema de pedidos — cada orden se gestiona en papel o verbalmente. La operación es completamente manual.",
                "Atención al cliente", "Implementar Gestión de Pedidos y conectar con mesas",
                "Operación manual completa, sin trazabilidad de ventas ni control de tiempos.",
                88),

            new(["order", "pedido"], ["menu", "dish", "carta", "plato"],
                "high", "Pedidos sin menú estructurado",
                "Existen pedidos pero sin un menú digital — los precios se manejan manualmente, no hay control de disponibilidad y es imposible analizar qué productos generan más ingresos.",
                "Menú y precios", "Crear Menú Digital con precios y categorías",
                "Precios inconsistentes, sin control de disponibilidad, imposible analizar rentabilidad por plato.",
                78),

            new(["inventor", "inventario", "stock"], ["supplier", "proveedor"],
                "medium", "Inventario sin proveedores vinculados",
                "El stock se registra pero los proveedores no están en el sistema — cada compra se gestiona con números de teléfono y sin historial de precios ni comparación de proveedores.",
                "Gestión de compras", "Registrar Proveedores y vincularlos con el inventario",
                "Compras reactivas, sin negociación informada, costos de insumos difíciles de controlar.",
                55),

            new(["inventor", "inventario", "stock"], ["purchase", "compra"],
                "medium", "Inventario sin órdenes de compra",
                "Hay control de stock pero sin órdenes de compra formales — cuando algo falta se compra de emergencia sin registro histórico ni proceso estandarizado.",
                "Gestión de compras", "Implementar módulo de Órdenes de Compra",
                "Compras de emergencia frecuentes, costos más altos, sin historial de aprovisionamiento.",
                50),

            new(["staff", "empleado", "personal", "waiter", "mesero"], ["payroll", "planilla", "salario"],
                "high", "Personal sin gestión de planilla",
                "El personal está registrado pero la planilla se calcula manualmente — cada periodo de pago requiere horas de trabajo administrativo con riesgo de errores.",
                "Gestión de personal", "Implementar módulo de Planilla",
                "Cálculos manuales cada quincena, riesgo de errores en salarios, insatisfacción del equipo.",
                68),

            new(["reservat", "reserva"], ["table", "mesa"],
                "medium", "Reservas sin control de mesas",
                "Hay módulo de reservaciones pero sin mapa de mesas digital — es imposible asignar y visualizar la ocupación real, generando doble-booking o espacios mal aprovechados.",
                "Gestión de capacidad", "Configurar Gestión de Mesas y vincular con reservaciones",
                "Doble-booking frecuente, baja utilización del espacio, mala experiencia del cliente.",
                48),
        ]
    );

    // ── HR / Payroll ─────────────────────────────────────────────────────────

    private static IndustryProfile HrPayrollProfile() => new(
        Workflows:
        [
            new("Flujo de Planilla", true,
                "Sin este flujo, el cálculo de salarios es manual y propenso a errores — generando conflictos laborales y pérdida de tiempo de RRHH.",
                [
                    new("Empleados",   ["employee", "empleado", "worker", "staff", "personal"]),
                    new("Asistencia",  ["attendan", "asistencia", "checkin", "timesheet"]),
                    new("Motor de Planilla", ["payroll", "planilla", "nomina", "salario", "salary"]),
                ]),

            new("Flujo de Ausencias", true,
                "Sin gestión de ausencias, los conflictos de cobertura se resuelven por WhatsApp — sin trazabilidad ni cumplimiento laboral.",
                [
                    new("Empleados",   ["employee", "empleado", "worker"]),
                    new("Ausencias / Vacaciones", ["leave", "vacacion", "absence", "ausencia"]),
                    new("Aprobaciones",            ["approv", "aprobacion", "workflow"]),
                ]),

            new("Flujo de Reportes", false,
                "Sin reportes, el gerente de RRHH no puede justificar costos ni identificar tendencias de rotación.",
                [
                    new("Planilla",    ["payroll", "planilla", "nomina"]),
                    new("Reportes",    ["report", "reporte", "analytic", "dashb"]),
                ]),
        ],

        BottleneckRules:
        [
            new(["attendan", "asistencia", "checkin", "timesheet"], ["payroll", "planilla", "nomina", "salario"],
                "critical", "Asistencia registrada sin motor de planilla",
                "El sistema captura asistencia pero no tiene motor de planilla — alguien debe exportar datos manualmente, calcular en Excel y subir resultados. Este proceso es lento, propenso a errores y ocurre cada quincena.",
                "Liquidación de salarios", "Implementar Motor de Planilla conectado con asistencia",
                "Cálculos manuales cada quincena, riesgo alto de errores, insatisfacción del equipo, posibles incumplimientos laborales.",
                95),

            new(["employee", "empleado", "worker", "staff", "personal"], ["attendan", "asistencia", "checkin", "timesheet"],
                "high", "Empleados sin control de asistencia digital",
                "Los empleados están registrados pero la asistencia se toma en papel o verbalmente — los datos de entrada/salida son imprecisos y la planilla se calcula con información incompleta.",
                "Control de tiempo", "Implementar Control de Asistencia (checkin/checkout digital)",
                "Planilla calculada con datos imprecisos, horas extra sin registro, posibles disputas laborales.",
                82),

            new(["employee", "empleado", "worker"], ["department", "departamento"],
                "medium", "Empleados sin estructura departamental",
                "No hay departamentos configurados — todos los empleados están al mismo nivel sin jerarquía. Es imposible generar reportes por área, asignar presupuestos o gestionar permisos granulares.",
                "Estructura organizacional", "Configurar Departamentos y asignar empleados",
                "Sin visibilidad de costos por área, imposible gestionar presupuestos departamentales.",
                52),

            new(["leave", "vacacion", "absence", "ausencia"], ["approv", "aprobacion", "workflow"],
                "medium", "Ausencias sin flujo de aprobación",
                "Hay módulo de vacaciones/ausencias pero las aprobaciones se hacen verbalmente o por mensaje — sin trazabilidad, sin historial y con riesgo de conflictos de cobertura no detectados a tiempo.",
                "Gestión de ausencias", "Implementar Flujo de Aprobaciones para ausencias",
                "Aprobaciones informales, conflictos de cobertura no detectados, sin auditoría de permisos.",
                58),

            new(["payroll", "planilla", "nomina"], ["report", "reporte", "analytic"],
                "medium", "Planilla sin reportes de gestión",
                "La planilla se procesa pero los resultados no generan reportes automáticos — el gerente de RRHH no puede analizar tendencias de costo, rotación o ausentismo sin exportar manualmente.",
                "Analytics de RRHH", "Implementar módulo de Reportes de Planilla",
                "Decisiones de RRHH sin respaldo de datos, costos laborales difíciles de proyectar.",
                48),

            new(["payroll", "planilla", "nomina"], ["audit", "auditoria", "log"],
                "medium", "Planilla sin auditoría",
                "Se procesan pagos pero sin registro de quién modificó qué y cuándo — en caso de disputa laboral o auditoría externa, no hay historial trazable de los cálculos.",
                "Cumplimiento y auditoría", "Habilitar Módulo de Auditoría para trazabilidad",
                "Sin defensa ante disputas laborales, riesgo de incumplimiento regulatorio.",
                45),
        ]
    );

    // ── Veterinary ───────────────────────────────────────────────────────────

    private static IndustryProfile VeterinaryProfile() => new(
        Workflows:
        [
            new("Flujo Clínico", true,
                "Sin historia clínica completa, cada consulta comienza desde cero — el veterinario no sabe el historial del paciente y el riesgo de error médico aumenta.",
                [
                    new("Pacientes / Mascotas",  ["patient", "paciente", "pet", "mascota", "animal"]),
                    new("Agenda de Citas",       ["appointment", "cita", "agenda", "consult"]),
                    new("Historia Clínica",      ["record", "histori", "expedient", "medical"]),
                    new("Tratamientos",          ["treatment", "tratamiento", "prescription", "receta"]),
                ]),

            new("Flujo de Negocio", true,
                "Sin facturación integrada, cada cobro es manual — sin control de ingresos ni trazabilidad de servicios prestados.",
                [
                    new("Citas",        ["appointment", "cita"]),
                    new("Facturación",  ["bill", "invoic", "factura", "payment", "pago"]),
                ]),

            new("Flujo de Seguimiento", false,
                "Sin recordatorios automáticos, los dueños olvidan vacunas y revisiones — aumenta el no-show y baja la retención de clientes.",
                [
                    new("Vacunas",          ["vaccin", "vacuna", "immuniz"]),
                    new("Notificaciones",   ["notif", "reminder", "recordatorio"]),
                ]),
        ],

        BottleneckRules:
        [
            new(["appointment", "cita", "agenda", "consult"], ["record", "histori", "expedient", "medical"],
                "critical", "Citas sin historia clínica",
                "Hay agenda de citas pero no existe historia clínica — en cada consulta el veterinario trabaja sin contexto del paciente. Sin saber alergias, tratamientos previos ni diagnósticos anteriores, el riesgo de error médico es real.",
                "Calidad clínica", "Implementar Historia Clínica y vincularla con las citas",
                "Tratamientos sin contexto médico, riesgo de prescribir medicamentos contraindicados, pérdida de historial al cambiar de veterinario.",
                93),

            new(["patient", "paciente", "pet", "mascota", "animal"], ["appointment", "cita", "agenda"],
                "high", "Pacientes sin agenda digital de citas",
                "Hay registro de pacientes pero sin sistema de citas — la agenda se maneja en cuaderno o por teléfono. El doble-booking, los olvidos y la falta de recordatorios afectan diariamente.",
                "Operaciones de la clínica", "Implementar Agenda de Citas vinculada con pacientes",
                "Doble-booking frecuente, no-shows sin gestión, carga administrativa alta.",
                80),

            new(["record", "histori", "expedient", "medical"], ["treatment", "tratamiento", "prescription", "receta"],
                "high", "Historia clínica sin prescripciones",
                "Existe historia clínica pero los tratamientos y medicamentos no se registran digitalmente — el historial está incompleto y es imposible detectar alergias o interacciones medicamentosas en consultas futuras.",
                "Seguridad del paciente", "Implementar módulo de Tratamientos / Prescripciones",
                "Medicación no trazable, riesgo de prescripciones contradictorias, historial clínico incompleto.",
                75),

            new(["appointment", "cita", "agenda", "consult"], ["bill", "invoic", "factura", "payment", "pago"],
                "high", "Citas sin facturación integrada",
                "Se agendan y completan citas pero el cobro se hace de forma manual y desconectada — los servicios prestados no quedan vinculados al cobro, generando inconsistencias y servicios sin facturar.",
                "Control financiero", "Integrar Facturación con el módulo de Citas",
                "Ingresos sin trazabilidad, servicios prestados sin cobrar, imposible auditar rentabilidad por veterinario.",
                72),

            new(["patient", "paciente", "pet", "mascota", "animal"], ["vaccin", "vacuna", "immuniz"],
                "medium", "Pacientes sin control de vacunas",
                "Hay registro de pacientes pero sin esquema de vacunación digital — los recordatorios dependen de que el dueño recuerde y los veterinarios no pueden ver proactivamente cuándo vence la próxima vacuna.",
                "Salud preventiva", "Implementar Control de Vacunas con recordatorios",
                "Baja adherencia a esquemas de vacunación, pérdida de ingresos por servicios preventivos no agendados.",
                55),

            new(["bill", "invoic", "factura", "payment", "pago"], ["report", "reporte", "analytic"],
                "medium", "Facturación sin reportes financieros",
                "Se factura pero sin reportes automáticos — el director no puede ver rentabilidad por servicio, ingresos por veterinario o tendencias de demanda sin exportar datos manualmente.",
                "Gestión financiera", "Implementar Reportes de Clínica",
                "Decisiones financieras sin datos, imposible identificar servicios más rentables.",
                48),
        ]
    );

    // ── E-Commerce ───────────────────────────────────────────────────────────

    private static IndustryProfile EcommerceProfile() => new(
        Workflows:
        [
            new("Flujo de Venta", true,
                "Sin este flujo completo, la tienda no puede procesar ventas digitales de forma autónoma.",
                [
                    new("Catálogo",     ["product", "catalog", "catalogo", "item", "sku"]),
                    new("Carrito/Órdenes",["order", "orden", "pedido"]),
                    new("Pasarela de Pago",["payment", "pago", "checkout", "stripe"]),
                ]),

            new("Flujo de Entrega", true,
                "Sin logística integrada, cada entrega requiere coordinación manual — clientes sin visibilidad y costos impredecibles.",
                [
                    new("Órdenes",  ["order", "orden", "pedido"]),
                    new("Envíos",   ["shipping", "envio", "delivery", "courier"]),
                ]),

            new("Flujo de Inventario", false,
                "Sin control de stock, el catálogo muestra productos que ya no existen — generando cancelaciones y mala experiencia.",
                [
                    new("Productos",   ["product", "catalog", "item"]),
                    new("Inventario",  ["inventor", "inventario", "stock", "warehou"]),
                ]),
        ],

        BottleneckRules:
        [
            new(["product", "catalog", "catalogo", "item", "sku"], ["order", "orden", "pedido"],
                "critical", "Catálogo sin sistema de órdenes",
                "Hay productos en el catálogo pero no existe sistema de órdenes — los clientes no pueden comprar. La tienda está vitrina pero no tienda.",
                "Proceso de venta", "Implementar Gestión de Órdenes / Carrito",
                "Cero ventas posibles, la inversión en catálogo no genera retorno.",
                95),

            new(["order", "orden", "pedido"], ["payment", "pago", "checkout", "stripe"],
                "critical", "Órdenes sin pasarela de pago",
                "Las órdenes se crean pero no hay integración de pagos — cada venta debe coordinarse manualmente para cobrar. Imposible escalar sin pago automático.",
                "Conversión de ventas", "Integrar Pasarela de Pago (Stripe o similar)",
                "Ventas interrumpidas antes del cobro, abandono masivo de carritos, imposible operar en línea.",
                92),

            new(["order", "orden", "pedido"], ["shipping", "envio", "delivery", "courier"],
                "high", "Órdenes pagadas sin logística integrada",
                "Las ventas se procesan pero la gestión de envíos es manual — el equipo coordina entregas por teléfono sin tracking ni registro de costos.",
                "Logística de entrega", "Implementar módulo de Envíos con tracking",
                "Clientes sin visibilidad de su pedido, costos logísticos difíciles de controlar, disputas frecuentes.",
                78),

            new(["product", "catalog", "item"], ["inventor", "inventario", "stock"],
                "high", "Catálogo sin control de stock",
                "Hay productos publicados pero sin inventario registrado — el sistema puede mostrar productos agotados como disponibles, generando ventas de productos inexistentes.",
                "Disponibilidad de productos", "Implementar Control de Inventario / Stock",
                "Overselling frecuente, cancelaciones post-venta, mala experiencia del cliente.",
                74),

            new(["customer", "cliente", "user", "buyer"], ["analytic", "report", "reporte"],
                "medium", "Clientes sin analytics de comportamiento",
                "Hay base de clientes pero sin análisis de comportamiento — el equipo no sabe qué productos se ven más, cuándo se abandona el carrito ni cuáles clientes son de mayor valor.",
                "Optimización de ventas", "Implementar Analytics de Ventas y Comportamiento",
                "Marketing sin datos, optimización del catálogo basada en intuición, LTV de clientes desconocido.",
                52),
        ]
    );

    // ── Logistics ────────────────────────────────────────────────────────────

    private static IndustryProfile LogisticsProfile() => new(
        Workflows:
        [
            new("Flujo de Envíos", true,
                "Sin este flujo, cada entrega es un proceso manual sin trazabilidad — imposible escalar.",
                [
                    new("Rutas",       ["route", "ruta", "zone", "zona"]),
                    new("Conductores", ["driver", "conductor", "repartidor"]),
                    new("Envíos",      ["shipment", "envio", "paquete", "package"]),
                ]),

            new("Flujo de Tracking", true,
                "Sin tracking, los clientes llaman constantemente para saber dónde está su paquete — genera carga operacional innecesaria.",
                [
                    new("Envíos",   ["shipment", "envio", "paquete"]),
                    new("Tracking", ["track", "gps", "location", "ubicacion"]),
                ]),

            new("Flujo de Facturación", false,
                "Sin facturación integrada, cada cobro a cliente es manual — riesgo de servicios sin facturar.",
                [
                    new("Envíos",      ["shipment", "envio"]),
                    new("Facturación", ["bill", "invoic", "factura", "tarifa", "rate"]),
                ]),
        ],

        BottleneckRules:
        [
            new(["route", "ruta", "zone", "zona"], ["shipment", "envio", "paquete", "package"],
                "critical", "Rutas definidas sin envíos registrados",
                "Las rutas de entrega están configuradas pero no se registran envíos digitalmente — los conductores salen sin control y la empresa no tiene trazabilidad de qué se entregó ni cuándo.",
                "Trazabilidad operacional", "Implementar módulo de Envíos y vincularlo con las rutas",
                "Sin trazabilidad de entregas, incapacidad de facturar por envío, quejas de clientes sin resolución.",
                90),

            new(["driver", "conductor", "repartidor"], ["shipment", "envio", "paquete"],
                "high", "Conductores sin asignación digital de envíos",
                "Los conductores están registrados pero la asignación de envíos se hace verbalmente o por teléfono — sin registro digital de quién lleva qué ni cuándo.",
                "Asignación de recursos", "Integrar asignación digital de envíos a conductores",
                "Sin responsabilidad trazable por entrega, imposible medir productividad por conductor.",
                80),

            new(["shipment", "envio", "paquete", "package"], ["track", "gps", "location"],
                "high", "Envíos sin tracking en tiempo real",
                "Los envíos se registran pero sin tracking — los clientes no tienen visibilidad y el equipo no puede reaccionar ante retrasos o problemas de entrega en tiempo real.",
                "Experiencia del cliente", "Implementar sistema de Tracking de envíos",
                "Alta carga de llamadas de soporte, clientes insatisfechos, imposible detectar retrasos proactivamente.",
                75),

            new(["shipment", "envio", "paquete"], ["bill", "invoic", "factura"],
                "high", "Entregas sin facturación automática",
                "Las entregas se completan pero la facturación a clientes es manual — existe riesgo de entregas no facturadas y el proceso de cobro genera trabajo administrativo innecesario.",
                "Control financiero", "Implementar Facturación automática por entrega",
                "Entregas sin cobrar, flujo de caja inconsistente, trabajo administrativo duplicado.",
                72),
        ]
    );

    // ── Real Estate ──────────────────────────────────────────────────────────

    private static IndustryProfile RealEstateProfile() => new(
        Workflows:
        [
            new("Flujo de Transacción", true,
                "Sin este flujo, cada negociación es manual — sin registro de contratos ni control de pagos.",
                [
                    new("Propiedades",  ["propert", "propiedad", "listing", "inmueble"]),
                    new("Clientes",     ["client", "cliente", "prospect", "lead"]),
                    new("Contratos",    ["contract", "contrato", "lease", "arrendam"]),
                ]),

            new("Flujo Comercial", true,
                "Sin pipeline de ventas, los prospectos se pierden y los agentes no tienen visibilidad de su embudo.",
                [
                    new("Clientes",     ["client", "cliente", "lead"]),
                    new("Visitas",      ["viewing", "visit", "visita", "showing"]),
                    new("Contratos",    ["contract", "contrato"]),
                ]),
        ],

        BottleneckRules:
        [
            new(["client", "cliente", "prospect", "lead"], ["contract", "contrato", "lease"],
                "critical", "Clientes sin generación de contratos",
                "Hay base de clientes/prospectos pero el sistema no genera contratos — cada negociación se cierra con documentos externos. Sin trazabilidad legal integrada.",
                "Cierre de negocios", "Implementar módulo de Contratos y vincular con clientes",
                "Negocios cerrados sin trazabilidad, documentos dispersos, riesgo legal.",
                88),

            new(["propert", "propiedad", "listing", "inmueble"], ["client", "cliente", "prospect"],
                "high", "Propiedades sin base de clientes",
                "El portafolio de propiedades está registrado pero no hay base de clientes/prospectos — las propiedades existen pero no hay pipeline de ventas para gestionarlas.",
                "Proceso de ventas", "Implementar CRM de Clientes y Prospectos",
                "Propiedades sin interesados registrados, oportunidades perdidas, sin métricas de conversión.",
                80),

            new(["contract", "contrato", "lease"], ["payment", "pago", "comision", "commission"],
                "high", "Contratos sin control de pagos",
                "Los contratos están registrados pero los pagos (rentas, cuotas, comisiones) se controlan externamente — riesgo de morosidad sin alertas y comisiones de agentes sin trazabilidad.",
                "Control financiero", "Implementar módulo de Pagos y Comisiones",
                "Morosidad sin detección temprana, comisiones calculadas manualmente, conflictos con agentes.",
                74),
        ]
    );

    // ── Healthcare ───────────────────────────────────────────────────────────

    private static IndustryProfile HealthcareProfile() => new(
        Workflows:
        [
            new("Flujo de Atención", true,
                "Sin expediente clínico vinculado a citas, los médicos trabajan sin contexto — riesgo médico y baja calidad de atención.",
                [
                    new("Pacientes",         ["patient", "paciente"]),
                    new("Citas",             ["appointment", "cita", "agenda", "schedule"]),
                    new("Expediente",        ["record", "expedient", "histori", "medical"]),
                    new("Prescripciones",    ["prescription", "receta", "medicament"]),
                ]),

            new("Flujo Financiero", true,
                "Sin facturación por consulta, los ingresos no son trazables y aseguradoras son difíciles de gestionar.",
                [
                    new("Citas",         ["appointment", "cita"]),
                    new("Facturación",   ["bill", "invoic", "factura", "insurance", "seguro"]),
                ]),
        ],

        BottleneckRules:
        [
            new(["patient", "paciente"], ["appointment", "cita", "agenda"],
                "critical", "Pacientes sin agenda de citas digital",
                "Hay pacientes registrados pero sin sistema de citas — la agenda se lleva en cuaderno o Excel. Doble-booking, tiempos de espera descontrolados y sin recordatorios automáticos.",
                "Operaciones de la clínica", "Implementar Agenda de Citas digital",
                "Doble-booking, tiempos de espera excesivos, pacientes que no regresan por mala experiencia.",
                88),

            new(["appointment", "cita", "agenda"], ["record", "expedient", "histori"],
                "critical", "Consultas sin expediente clínico",
                "Las citas se agendan pero sin expediente digital — los médicos trabajan sin historial del paciente en cada consulta. Diagnósticos sin contexto, riesgo de prescripciones contradictorias.",
                "Seguridad del paciente", "Implementar Expediente Clínico vinculado a citas",
                "Atención sin contexto, riesgo de errores médicos, expedientes en papel difíciles de auditar.",
                90),

            new(["appointment", "cita", "agenda"], ["bill", "invoic", "factura", "insurance"],
                "high", "Consultas sin facturación integrada",
                "Las citas se completan pero la facturación es manual y desconectada — cada consulta requiere un proceso adicional de cobro sin vinculación automática al servicio prestado.",
                "Control financiero", "Integrar Facturación con módulo de Citas",
                "Consultas sin cobrar, proceso de facturación lento, dificultad para tramitar seguros médicos.",
                78),

            new(["record", "expedient", "histori"], ["prescription", "receta", "medicament"],
                "high", "Expediente sin prescripciones digitales",
                "El expediente existe pero las recetas se escriben en papel — sin historial de medicación digital es imposible detectar interacciones y el paciente puede recibir medicamentos contradictorios.",
                "Seguridad de prescripciones", "Implementar Prescripciones digitales en el expediente",
                "Riesgo de interacciones medicamentosas, recetas pérdidas, historial de medicación incompleto.",
                75),
        ]
    );

    // ── Education ────────────────────────────────────────────────────────────

    private static IndustryProfile EducationProfile() => new(
        Workflows:
        [
            new("Flujo Académico", true,
                "Sin calificaciones vinculadas a matrículas, el proceso de evaluación es manual y los boletines requieren horas de trabajo.",
                [
                    new("Estudiantes",  ["student", "estudiante", "alumno"]),
                    new("Cursos",       ["course", "curso", "materia", "class"]),
                    new("Matrículas",   ["enroll", "matricul", "registr"]),
                    new("Calificaciones",["grade", "calificacion", "nota", "score"]),
                ]),

            new("Flujo Financiero", true,
                "Sin control de pagos, los adeudos se acumulan sin alertas — el flujo de caja de la institución es impredecible.",
                [
                    new("Matrículas",  ["enroll", "matricul"]),
                    new("Pagos",       ["payment", "pago", "colegiatura", "tuition"]),
                ]),
        ],

        BottleneckRules:
        [
            new(["student", "estudiante", "alumno"], ["enroll", "matricul", "registr"],
                "critical", "Estudiantes sin sistema de matrículas",
                "Hay estudiantes registrados pero sin control de matrículas — los cupos se manejan manualmente, sin historial de inscripciones y sin vinculación a cursos específicos.",
                "Gestión académica", "Implementar módulo de Matrículas",
                "Cupos mal controlados, listas por grupo incompletas, facturación de inscripciones manual.",
                88),

            new(["enroll", "matricul", "registr"], ["grade", "calificacion", "nota"],
                "high", "Matrículas sin sistema de calificaciones",
                "Los estudiantes están matriculados pero las calificaciones se registran en papel o Excel — los boletines requieren trabajo manual y los padres no tienen visibilidad en tiempo real.",
                "Proceso de evaluación", "Implementar módulo de Calificaciones vinculado a matrículas",
                "Boletines manuales propensos a errores, sin alertas de estudiantes en riesgo académico.",
                80),

            new(["enroll", "matricul"], ["payment", "pago", "colegiatura", "tuition"],
                "high", "Matrículas sin control de pagos",
                "Las matrículas se registran pero los pagos de colegiatura son manuales — los adeudos se acumulan sin alertas automáticas y el flujo de caja de la institución es difícil de proyectar.",
                "Control financiero", "Implementar Gestión de Pagos / Colegiaturas",
                "Adeudos sin alertas, flujo de caja impredecible, carga administrativa en cobranza.",
                75),

            new(["student", "estudiante", "alumno"], ["attendan", "asistencia", "presence"],
                "medium", "Estudiantes sin control de asistencia",
                "Sin control digital de asistencia, la detección de abandono escolar es tardía — cuando el director se da cuenta, el estudiante ya dejó de asistir hace semanas.",
                "Retención estudiantil", "Implementar Control de Asistencia",
                "Detección tardía de abandono, sin alertas a padres, incumplimiento de requisito mínimo de asistencia.",
                58),
        ]
    );

    // ── General ──────────────────────────────────────────────────────────────

    private static IndustryProfile GeneralProfile() => new(
        Workflows:
        [
            new("Flujo Core", true,
                "Sin un módulo central, el sistema no tiene valor que ofrecer a sus usuarios.",
                [
                    new("Módulo Principal", ["product", "item", "entity", "service", "recurso", "catalog"]),
                    new("Usuarios",         ["user", "usuario", "member", "account"]),
                ]),

            new("Flujo de Monetización", false,
                "Sin facturación integrada, el SaaS no puede generar ingresos de forma autónoma.",
                [
                    new("Usuarios",     ["user", "usuario", "account"]),
                    new("Facturación",  ["bill", "subscript", "payment", "pago", "invoic", "stripe"]),
                ]),
        ],

        BottleneckRules:
        [
            new(["user", "usuario", "member", "account"], ["bill", "subscript", "payment", "pago", "invoic"],
                "high", "Usuarios sin sistema de facturación",
                "El sistema tiene usuarios pero no puede cobrarles automáticamente — la monetización es manual, imposible escalar y difícil de controlar.",
                "Monetización", "Integrar sistema de Facturación / Suscripciones",
                "Sin ingresos automatizados, churn difícil de detectar, escalabilidad limitada.",
                78),

            new(["product", "item", "entity", "service", "catalog"], ["report", "analytic", "reporte"],
                "medium", "Módulo principal sin reportes",
                "El módulo central existe pero sin reportes — los usuarios no pueden ver el valor generado y la dirección no tiene visibilidad del uso.",
                "Visibilidad del negocio", "Implementar Reportes y Analytics del módulo principal",
                "Valor entregado invisible para usuarios y dirección, decisiones basadas en intuición.",
                55),

            new(["user", "usuario", "account"], ["notif", "email", "alert"],
                "medium", "Sistema sin notificaciones a usuarios",
                "Hay usuarios pero el sistema es completamente pasivo — sin notificaciones, los usuarios deben entrar activamente para ver novedades. La retención sufre.",
                "Engagement de usuarios", "Implementar sistema de Notificaciones",
                "Baja retención de usuarios, dependencia de visitas activas para generar valor.",
                50),
        ]
    );
}
