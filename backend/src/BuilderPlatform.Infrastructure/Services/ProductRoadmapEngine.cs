using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuilderPlatform.Infrastructure.Services;

// ── Domain records ──────────────────────────────────────────────────────────

public record RoadmapMilestone(
    string Id,
    string Title,
    string Phase,      // "now" | "next" | "later"
    string Priority,   // "critical" | "high" | "medium"
    string Category,   // "core" | "integration" | "growth" | "analytics"
    string Why,
    string Unlocks,
    List<string> RequiredModules
);

public record RoadmapDependency(
    string From,
    string To,
    string Reason
);

public record StrategicRoadmap(
    string                  ProductId,
    string                  Industry,
    string                  IndustryLabel,
    int                     CompletionScore,
    int                     TotalCheckpoints,
    int                     CompletedCheckpoints,
    string                  GrowthNarrative,
    string                  NextFocusTitle,
    string                  NextFocusWhy,
    List<RoadmapMilestone>  Milestones,
    List<RoadmapDependency> Dependencies,
    DateTime                GeneratedAt
);

// ── Engine ──────────────────────────────────────────────────────────────────

public class ProductRoadmapEngine
{
    public async Task<StrategicRoadmap> GenerateAsync(Guid productId, AppDbContext db, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Modules)
            .Include(p => p.Memory)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null) return Empty(productId);

        var industryKey = product.Memory
            .FirstOrDefault(m => m.Key == "industry")?.Value ?? "general";
        var activeNames = product.Modules
            .Where(m => m.IsActive)
            .Select(m => m.ModuleName.ToLowerInvariant())
            .ToList();

        var profile  = GetProfile(industryKey);
        var evaluated = profile.Checkpoints.Select(cp => (cp, exists: Exists(cp.Keywords, activeNames))).ToList();

        var completed = evaluated.Count(e => e.exists);
        var total     = profile.Checkpoints.Count;
        var score     = total == 0 ? 0 : Math.Min(100, completed * 100 / total);

        // Build milestones from incomplete checkpoints
        var milestones = new List<RoadmapMilestone>();
        foreach (var (cp, exists) in evaluated)
        {
            if (exists) continue;

            var depsComplete = cp.Requires.All(req => evaluated.Any(e => e.cp.Id == req && e.exists));

            var phase = (!depsComplete)            ? "later"
                      : (cp.IsCore)               ? "now"
                      : (cp.Category == "growth")  ? "next"
                                                   : "next";

            // Promote to "now" if there are no core modules at all
            if (phase == "next" && completed == 0) phase = "now";

            var requiredLabels = cp.Requires
                .Select(req => profile.Checkpoints.FirstOrDefault(c => c.Id == req)?.Title ?? req)
                .ToList();

            milestones.Add(new RoadmapMilestone(
                cp.Id, cp.Title, phase, cp.Priority, cp.Category,
                cp.Why, cp.Unlocks, requiredLabels
            ));
        }

        milestones = milestones
            .OrderBy(m => PhaseOrder(m.Phase))
            .ThenBy(m => PriorityOrder(m.Priority))
            .Take(8)
            .ToList();

        // Build dependencies from module relationships
        var deps = profile.Dependencies
            .Where(d =>
            {
                var fromExists = evaluated.Any(e => e.cp.Id == d.FromId && e.exists);
                var toMissing  = evaluated.Any(e => e.cp.Id == d.ToId   && !e.exists);
                return fromExists && toMissing;
            })
            .Select(d =>
            {
                var fromLabel = profile.Checkpoints.FirstOrDefault(c => c.Id == d.FromId)?.Title ?? d.FromId;
                var toLabel   = profile.Checkpoints.FirstOrDefault(c => c.Id == d.ToId)?.Title   ?? d.ToId;
                return new RoadmapDependency(fromLabel, toLabel, d.Reason);
            })
            .Take(5)
            .ToList();

        var top       = milestones.FirstOrDefault();
        var narrative = BuildNarrative(industryKey, score, completed, total, product.Modules.Count(m => m.IsActive));

        return new StrategicRoadmap(
            productId.ToString(), industryKey, IndustryLabel(industryKey),
            score, total, completed,
            narrative,
            top?.Title   ?? "Definir módulos base",
            top?.Why     ?? "El sistema necesita módulos base para comenzar a operar.",
            milestones, deps,
            DateTime.UtcNow
        );
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static bool Exists(string[] keywords, List<string> moduleNames) =>
        keywords.Any(k => moduleNames.Any(m => m.Contains(k)));

    private static int PhaseOrder(string phase) => phase switch { "now" => 0, "next" => 1, _ => 2 };
    private static int PriorityOrder(string p)  => p switch { "critical" => 0, "high" => 1, _ => 2 };

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
        "gaming"      => "Gaming",
        _             => "General",
    };

    private static string BuildNarrative(string industry, int score, int done, int total, int moduleCount)
    {
        var pct  = total == 0 ? 0 : done * 100 / total;
        var label = industry switch
        {
            "restaurant" => "restaurante",
            "hr_payroll" => "sistema de RRHH",
            "veterinary" => "clínica veterinaria",
            "ecommerce"  => "tienda en línea",
            "logistics"  => "operación logística",
            _            => "sistema SaaS",
        };

        return (score, moduleCount) switch
        {
            ( < 20, 0) => $"El {label} está en etapa inicial. Construir los módulos core es la prioridad inmediata — sin ellos no hay operación posible.",
            ( < 20, _) => $"El {label} tiene {moduleCount} módulo{(moduleCount == 1 ? "" : "s")} activo{(moduleCount == 1 ? "" : "s")} pero le faltan los pilares operacionales. Los módulos core deben completarse antes de cualquier crecimiento.",
            ( < 50, _) => $"El {label} tiene una base inicial ({pct}% completado). El siguiente paso es cerrar los gaps operacionales críticos que hoy limitan la eficiencia diaria.",
            ( < 75, _) => $"El {label} está en etapa de crecimiento ({pct}% completado). La prioridad ahora es conectar módulos existentes e integrar funciones de reporting y automatización.",
            _          => $"El {label} tiene una base operacional sólida ({pct}% completado). El roadmap se orienta hacia optimización, analytics avanzados y experiencias diferenciadas.",
        };
    }

    private static StrategicRoadmap Empty(Guid productId) =>
        new(productId.ToString(), "general", "General",
            0, 0, 0,
            "El sistema está en etapa inicial. Define módulos para generar un roadmap contextual.",
            "Agregar módulos base",
            "Sin módulos no es posible generar un roadmap operacional.",
            [], [], DateTime.UtcNow);

    // ── Industry profiles ────────────────────────────────────────────────────

    private sealed record Checkpoint(
        string   Id,
        string   Title,
        string[] Keywords,
        bool     IsCore,
        string   Priority,
        string   Category,
        string   Why,
        string   Unlocks,
        string[] Requires
    );

    private sealed record DepRule(string FromId, string ToId, string Reason);

    private sealed record IndustryProfile(List<Checkpoint> Checkpoints, List<DepRule> Dependencies);

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

    // ── Restaurant ─────────────────────────────────────────────────────────

    private static IndustryProfile RestaurantProfile() => new(
        [
            new("orders",      "Gestión de Pedidos",    ["order", "pedido"],               true,  "critical", "core",
                "Sin pedidos no hay operación. Es el corazón de cualquier restaurante.",
                "Base para facturación, cocina y métricas de venta.",
                []),

            new("tables",      "Gestión de Mesas",      ["table", "mesa"],                 true,  "critical", "core",
                "El mapa de mesas permite asignar pedidos, controlar turnos y optimizar el espacio.",
                "Conecta pedidos con espacios físicos y mejora tiempos de servicio.",
                []),

            new("menu",        "Menú y Carta Digital",  ["menu", "dish", "carta", "plato", "product"], true, "high", "core",
                "Sin menú estructurado los pedidos no tienen base de precios ni categorías.",
                "Habilita control de precios, disponibilidad y variantes por plato.",
                []),

            new("inventory",   "Control de Inventario", ["inventor", "inventario", "stock"], true, "high", "core",
                "Sin inventario el restaurante opera a ciegas — sin saber qué tiene ni qué va a necesitar.",
                "Previene desabasto, reduce desperdicio y habilita órdenes de compra.",
                []),

            new("kitchen",     "Gestión de Cocina/KDS", ["kitchen", "kds", "cocina"],       true, "high", "core",
                "La comunicación sala-cocina hoy se hace en papel o a gritos. Un KDS elimina errores y mejora tiempos.",
                "Reduce errores de pedido, mejora tiempos de preparación, desbloquea métricas de cocina.",
                ["orders"]),

            new("staff",       "Personal y Turnos",     ["staff", "empleado", "personal", "waiter", "mesero", "employee"], false, "high", "growth",
                "Controlar turnos y asistencia del personal permite reducir costos y mejorar cobertura.",
                "Base para planilla, productividad por turno y métricas de equipo.",
                []),

            new("suppliers",   "Proveedores",           ["supplier", "proveedor"],           false, "medium", "growth",
                "Centralizar proveedores permite negociar mejor y tener trazabilidad de compras.",
                "Habilita órdenes de compra automáticas y evaluación de proveedores.",
                ["inventory"]),

            new("purchases",   "Órdenes de Compra",     ["purchase", "compra"],             false, "medium", "integration",
                "Automatizar compras en base al inventario reduce tiempo administrativo y costos.",
                "Integra proveedores con inventario, elimina compras de emergencia.",
                ["inventory", "suppliers"]),

            new("reservations","Reservaciones",          ["reservat", "reserva"],             false, "medium", "growth",
                "Gestionar reservas reduce esperas y mejora la experiencia del cliente.",
                "Permite planificar capacidad y reducir mesas vacías en horas pico.",
                ["tables"]),

            new("customers",   "CRM de Clientes",       ["customer", "cliente", "crm"],      false, "medium", "growth",
                "Conocer al cliente permite personalizar, fidelizar y aumentar ticket promedio.",
                "Base para programas de lealtad, marketing y análisis de comportamiento.",
                []),

            new("analytics",   "Reportes y Analytics",  ["analytic", "report", "reporte", "dashb", "metric"], false, "medium", "analytics",
                "Sin datos el dueño toma decisiones basadas en intuición. Los reportes cambian eso.",
                "Visibilidad de ventas, rentabilidad, productos estrella y tendencias.",
                ["orders"]),

            new("notifications","Notificaciones",        ["notif"],                           false, "low", "integration",
                "Alertas automáticas de bajo inventario, pedidos pendientes y reservas reducen errores operacionales.",
                "Menos intervención manual, respuesta más rápida a eventos críticos.",
                []),
        ],
        [
            new("orders",    "kitchen",     "Pedidos activos → Cocina necesita ver y gestionar esos pedidos en tiempo real"),
            new("inventory", "purchases",   "Inventario existente → Compras pueden automatizarse cuando el stock baje del mínimo"),
            new("inventory", "suppliers",   "Inventario registrado → Proveedores pueden asociarse a insumos específicos"),
            new("tables",    "reservations","Mesas configuradas → Reservaciones pueden asignarse a mesas específicas"),
            new("customers", "analytics",   "Base de clientes → Analytics puede segmentar ventas por perfil de cliente"),
        ]
    );

    // ── HR / Payroll ────────────────────────────────────────────────────────

    private static IndustryProfile HrPayrollProfile() => new(
        [
            new("employees",   "Gestión de Empleados",  ["employee", "empleado", "worker", "staff", "personal"], true, "critical", "core",
                "Sin registro de empleados no hay planilla ni control de asistencia posible.",
                "Base de todo el sistema: planilla, asistencia, departamentos y roles.",
                []),

            new("departments", "Departamentos y Puestos",["department", "departamento", "position", "cargo", "puesto"], true, "high", "core",
                "Estructurar la organización por departamentos permite gestionar jerarquías y reportes.",
                "Habilita asignación de permisos, reportes por área y presupuesto por departamento.",
                ["employees"]),

            new("attendance",  "Control de Asistencia", ["attendan", "asistencia", "checkin", "timesheet", "tiempo"], true, "critical", "core",
                "Sin asistencia registrada la planilla quincena es manual y propensa a errores.",
                "Base obligatoria para calcular salarios, horas extra y ausencias.",
                ["employees"]),

            new("payroll",     "Motor de Planilla",     ["payroll", "planilla", "nomina", "salario", "salary", "wage"], true, "critical", "core",
                "La planilla es el corazón del sistema. Sin ella los empleados no cobran a tiempo.",
                "Automatiza liquidaciones, horas extra, deducciones y genera recibos de salario.",
                ["employees", "attendance"]),

            new("leave",       "Gestión de Ausencias",  ["leave", "vacacion", "absence", "ausencia", "permiso"], false, "high", "growth",
                "Sin gestión de ausencias los supervisores no tienen visibilidad de disponibilidad real del equipo.",
                "Elimina conflictos de cobertura, automatiza aprobaciones y conecta con planilla.",
                ["employees", "attendance"]),

            new("approvals",   "Flujo de Aprobaciones", ["approv", "aprobacion", "workflow", "flujo"], false, "high", "integration",
                "Sin flujo de aprobaciones los permisos y ausencias se gestionan por WhatsApp o email.",
                "Proceso trazable, auditable y escalable para vacaciones, gastos y cambios de turno.",
                ["leave"]),

            new("recruitment", "Reclutamiento",         ["recruit", "reclut", "candidat", "hiring", "aplicant"], false, "medium", "growth",
                "Centralizar candidatos y procesos de selección reduce tiempo de contratación.",
                "Pipeline de candidatos, evaluaciones y onboarding automático.",
                ["employees", "departments"]),

            new("performance", "Evaluación de Desempeño",["performan", "desempeno", "evaluacion", "review", "kpi"], false, "medium", "growth",
                "Sin evaluaciones el crecimiento del empleado es opaco y los bonos no tienen base objetiva.",
                "Métricas de desempeño, planes de desarrollo y base para bonificaciones.",
                ["employees"]),

            new("audit",       "Auditoría y Logs",      ["audit", "auditoria", "log", "historial"], false, "medium", "integration",
                "En RRHH toda acción debe ser trazable. La auditoría protege a la empresa y al empleado.",
                "Cumplimiento legal, resolución de disputas y trazabilidad completa de cambios.",
                ["payroll"]),

            new("reports",     "Reportes y Analytics",  ["report", "reporte", "analytic", "dashb"], false, "medium", "analytics",
                "Sin reportes el gerente de RRHH no puede justificar costos ni identificar tendencias.",
                "Costo de planilla por área, ausentismo, rotación y proyecciones de headcount.",
                ["payroll", "attendance"]),
        ],
        [
            new("attendance",  "payroll",    "Asistencia registrada → Planilla puede calcularse automáticamente sin ajustes manuales"),
            new("employees",   "payroll",    "Empleados con roles y salarios → Motor de planilla tiene todos los datos necesarios"),
            new("leave",       "approvals",  "Ausencias registradas → Flujo de aprobaciones puede automatizar solicitudes pendientes"),
            new("payroll",     "audit",      "Planilla procesada → Auditoría puede trazar cada cambio y corrección"),
            new("attendance",  "reports",    "Asistencia histórica → Reportes pueden mostrar ausentismo y tendencias por equipo"),
        ]
    );

    // ── Veterinary ─────────────────────────────────────────────────────────

    private static IndustryProfile VeterinaryProfile() => new(
        [
            new("patients",   "Pacientes / Mascotas",   ["patient", "paciente", "pet", "mascota", "animal"], true, "critical", "core",
                "El registro de mascotas es el núcleo de cualquier clínica veterinaria.",
                "Base para historial médico, citas y recordatorios de vacunas.",
                []),

            new("owners",     "Dueños / Clientes",      ["owner", "dueno", "propietario", "client", "cliente"], true, "high", "core",
                "Los dueños son los clientes reales. Sin registro de propietarios no hay facturación ni contacto.",
                "Permite comunicación, facturación y fidelización de clientes recurrentes.",
                []),

            new("appointments","Agenda de Citas",        ["appointment", "cita", "agenda", "consult"], true, "critical", "core",
                "Sin agenda digital las citas se manejan en cuaderno. Hay doble-booking, olvidos y cancelaciones sin aviso.",
                "Control de disponibilidad, recordatorios automáticos y agenda del veterinario.",
                ["patients", "owners"]),

            new("records",    "Historia Clínica",        ["record", "histor", "expedient", "medical", "clinico"], true, "critical", "core",
                "La historia clínica es legalmente obligatoria y operacionalmente crítica para tratar bien al paciente.",
                "Trazabilidad de diagnósticos, tratamientos y evolución del paciente.",
                ["patients", "appointments"]),

            new("vaccines",   "Control de Vacunas",     ["vaccin", "vacuna", "immuniz"], false, "high", "growth",
                "Sin control de vacunas los veterinarios dependen de que el dueño recuerde cuándo vence la próxima dosis.",
                "Recordatorios automáticos al dueño, historial completo y cumplimiento de esquemas.",
                ["patients"]),

            new("treatments", "Tratamientos y Recetas", ["treatment", "tratamiento", "prescription", "receta", "medicament"], false, "high", "integration",
                "Registrar tratamientos y medicamentos hace el expediente trazable y reduce errores de prescripción.",
                "Historial de medicación, alertas de alergias y seguimiento post-consulta.",
                ["records"]),

            new("billing",    "Facturación",             ["bill", "invoic", "factura", "payment", "pago", "cobro"], false, "high", "core",
                "Sin facturación integrada cada cobro es manual y los ingresos no tienen trazabilidad.",
                "Control de ingresos, deudas de clientes y reportes financieros.",
                ["appointments"]),

            new("inventory",  "Farmacia / Inventario",  ["inventor", "inventario", "pharmacy", "farmacia", "insumo"], false, "medium", "growth",
                "Sin control de inventario la clínica no sabe qué medicamentos tiene ni cuándo pedir más.",
                "Control de stock de medicamentos, alertas de vencimiento y costo de insumos.",
                []),

            new("notifications","Recordatorios",         ["notif", "reminder", "recordatorio", "alert"], false, "medium", "integration",
                "Los recordatorios automáticos reducen citas perdidas y mejoran adherencia a tratamientos.",
                "Reducción de no-shows, mejor salud animal y experiencia del cliente.",
                ["appointments"]),

            new("reports",    "Reportes de Clínica",    ["report", "reporte", "analytic", "estadis"], false, "medium", "analytics",
                "Sin reportes el veterinario no sabe qué servicios generan más ingresos ni qué pacientes son de riesgo.",
                "Rentabilidad por servicio, patologías frecuentes y métricas de la clínica.",
                ["billing", "records"]),
        ],
        [
            new("patients",    "records",       "Pacientes registrados → Historia clínica puede asociarse a cada animal con trazabilidad completa"),
            new("appointments","records",       "Citas agendadas → Historia clínica puede documentarse al momento de la consulta"),
            new("patients",    "vaccines",      "Pacientes registrados → Control de vacunas puede gestionar el esquema de inmunización por especie"),
            new("billing",     "reports",       "Facturación registrada → Reportes muestran rentabilidad real por servicio y veterinario"),
            new("appointments","notifications", "Citas agendadas → Recordatorios automáticos pueden enviarse al dueño 24h antes"),
        ]
    );

    // ── E-Commerce ─────────────────────────────────────────────────────────

    private static IndustryProfile EcommerceProfile() => new(
        [
            new("products",   "Catálogo de Productos",  ["product", "catalog", "catalogo", "item", "sku"], true, "critical", "core",
                "Sin catálogo no hay tienda. Es el punto de partida de cualquier e-commerce.",
                "Base para ventas, inventario y búsqueda de productos.",
                []),

            new("orders",     "Gestión de Órdenes",     ["order", "orden", "pedido", "purchase"], true, "critical", "core",
                "Las órdenes son el corazón del e-commerce. Cada venta es una orden.",
                "Trazabilidad de ventas, historial del cliente y base para logística.",
                ["products"]),

            new("inventory",  "Inventario y Stock",     ["inventor", "inventario", "stock", "warehou"], true, "high", "core",
                "Sin control de stock la tienda puede vender productos que no existen.",
                "Previene overselling, controla reabastecimiento y gestiona múltiples almacenes.",
                ["products"]),

            new("customers",  "CRM de Clientes",        ["customer", "cliente", "user", "buyer", "comprador"], true, "high", "core",
                "Conocer al cliente permite personalizar, recuperar carritos y fidelizar.",
                "Base para segmentación, marketing y programas de lealtad.",
                []),

            new("payments",   "Pagos y Checkout",       ["payment", "pago", "checkout", "stripe", "billing"], true, "critical", "core",
                "Sin pasarela de pago integrada la tienda no puede procesar ventas.",
                "Pagos en línea, manejo de reembolsos y prevención de fraude.",
                ["orders"]),

            new("shipping",   "Envíos y Logística",     ["shipping", "envio", "delivery", "courier", "logistic"], false, "high", "integration",
                "Sin gestión de envíos las órdenes quedan en el aire después del pago.",
                "Seguimiento de paquetes, cálculo de costos y notificaciones al cliente.",
                ["orders"]),

            new("reviews",    "Reseñas y Ratings",      ["review", "resena", "rating", "comment"], false, "medium", "growth",
                "Las reseñas generan confianza y mejoran la conversión de nuevos compradores.",
                "Prueba social, retroalimentación de producto y SEO orgánico.",
                ["products", "customers"]),

            new("analytics",  "Analytics de Ventas",   ["analytic", "report", "reporte", "dashb", "metric"], false, "medium", "analytics",
                "Sin analytics el equipo no sabe qué productos venden más ni por qué se abandonan carritos.",
                "Optimización de catálogo, campañas basadas en datos y proyecciones de inventario.",
                ["orders", "customers"]),

            new("notifications","Notificaciones",        ["notif", "email", "alert", "reminder"], false, "medium", "integration",
                "Los correos transaccionales (confirmación, envío, entrega) son parte de la experiencia de compra.",
                "Mejor retención, menor tasa de disputas y upsell automático.",
                ["orders"]),
        ],
        [
            new("orders",    "shipping",      "Órdenes pagadas → Envíos pueden vincularse automáticamente con número de guía y seguimiento"),
            new("products",  "inventory",     "Catálogo definido → Inventario puede controlar stock por SKU y prevenir overselling"),
            new("customers", "analytics",     "Base de clientes → Analytics puede segmentar comportamiento de compra y LTV"),
            new("orders",    "notifications", "Órdenes creadas → Notificaciones pueden enviarse en cada cambio de estado del pedido"),
            new("inventory", "analytics",     "Inventario registrado → Analytics puede predecir agotamiento y recomendar reabastecimiento"),
        ]
    );

    // ── Logistics ──────────────────────────────────────────────────────────

    private static IndustryProfile LogisticsProfile() => new(
        [
            new("routes",     "Rutas y Zonas",          ["route", "ruta", "zone", "zona", "area"], true, "critical", "core",
                "Sin rutas definidas los repartidores operan sin estructura y el costo por entrega es impredecible.",
                "Base para asignación de conductores, optimización y cálculo de costos.",
                []),

            new("drivers",    "Conductores",            ["driver", "conductor", "repartidor", "courier"], true, "critical", "core",
                "Sin registro de conductores no hay asignación de rutas ni control de desempeño.",
                "Permite asignar rutas, monitorear desempeño y gestionar pagos por entrega.",
                []),

            new("shipments",  "Envíos y Paquetes",      ["shipment", "envio", "paquete", "package", "parcel"], true, "critical", "core",
                "El core de una empresa logística son sus envíos. Sin registro no hay trazabilidad.",
                "Tracking de paquetes, historial de entregas y base para facturación.",
                ["routes", "drivers"]),

            new("vehicles",   "Flota de Vehículos",     ["vehicle", "vehiculo", "truck", "camion", "fleet", "flota"], true, "high", "core",
                "Sin control de flota no se puede gestionar mantenimiento ni asignar vehículos por capacidad.",
                "Asignación óptima de vehículos, alertas de mantenimiento y control de costos de flota.",
                ["drivers"]),

            new("tracking",   "Tracking en Tiempo Real",["track", "gps", "location", "ubicacion", "realtime"], false, "high", "growth",
                "Los clientes hoy esperan saber dónde está su paquete en todo momento.",
                "Reduce llamadas de soporte, mejora NPS y permite reaccionar a problemas en tiempo real.",
                ["shipments", "drivers"]),

            new("customers",  "Clientes",               ["customer", "cliente", "remitente", "sender"], false, "high", "core",
                "Sin registro de clientes la facturación es manual y no hay trazabilidad por cuenta.",
                "Historial de envíos por cliente, facturación automática y gestión de contratos.",
                []),

            new("billing",    "Facturación",            ["bill", "invoic", "factura", "payment", "tarifa", "rate"], false, "high", "integration",
                "Sin facturación integrada cada cobro a cliente es un proceso manual.",
                "Tarifas por zona/peso, facturación automática y control de cuentas por cobrar.",
                ["shipments", "customers"]),

            new("analytics",  "Analytics Operacional",  ["analytic", "report", "reporte", "kpi", "dashb"], false, "medium", "analytics",
                "Sin métricas no se puede optimizar rutas, identificar cuellos de botella ni justificar inversión.",
                "Eficiencia por ruta, costo por entrega, desempeño por conductor y tendencias.",
                ["shipments", "routes"]),
        ],
        [
            new("routes",   "shipments", "Rutas configuradas → Envíos pueden asignarse automáticamente a la ruta óptima"),
            new("drivers",  "shipments", "Conductores registrados → Envíos pueden asignarse y monitorearse por conductor"),
            new("shipments","tracking",  "Envíos activos → Tracking puede actualizar ubicación y estado en tiempo real"),
            new("shipments","billing",   "Envíos completados → Facturación puede generarse automáticamente con tarifas por zona"),
            new("shipments","analytics", "Historial de envíos → Analytics puede calcular eficiencia y costo por ruta"),
        ]
    );

    // ── Real Estate ────────────────────────────────────────────────────────

    private static IndustryProfile RealEstateProfile() => new(
        [
            new("properties", "Propiedades",            ["propert", "propiedad", "listing", "inmueble", "asset"], true, "critical", "core",
                "Sin catálogo de propiedades no hay nada que mostrar ni gestionar.",
                "Base para búsquedas, contratos y métricas del portafolio.",
                []),

            new("clients",    "Clientes / Prospectos",  ["client", "cliente", "prospect", "lead", "comprador"], true, "critical", "core",
                "Sin registro de clientes no hay seguimiento de interesados ni historial de negociaciones.",
                "Pipeline de ventas, seguimiento de visitas y cierre de negocios.",
                []),

            new("contracts",  "Contratos",              ["contract", "contrato", "lease", "arrendam"], true, "critical", "core",
                "Los contratos son el documento legal del negocio. Sin ellos la operación no tiene respaldo.",
                "Trazabilidad legal, fechas de vencimiento y alertas de renovación.",
                ["properties", "clients"]),

            new("viewings",   "Visitas y Agenda",       ["viewing", "visit", "visita", "showing", "tour"], false, "high", "growth",
                "Sin agenda de visitas los agentes coordinan por WhatsApp y pierden prospectos interesados.",
                "Seguimiento de interés, eficiencia del agente y pipeline de conversión.",
                ["properties", "clients"]),

            new("agents",     "Agentes Inmobiliarios",  ["agent", "agente", "broker", "asesor"], false, "high", "core",
                "Sin registro de agentes no se puede medir desempeño ni asignar propiedades.",
                "Comisiones, portafolio por agente y métricas de conversión.",
                []),

            new("payments",   "Pagos y Comisiones",     ["payment", "pago", "comision", "commission", "rent"], false, "high", "integration",
                "Sin registro de pagos los ingresos no son trazables y los arrendadores no confían en el sistema.",
                "Control de rentas, comisiones de agentes y morosidad.",
                ["contracts"]),

            new("analytics",  "Reportes del Portafolio",["analytic", "report", "reporte", "dashb", "metric"], false, "medium", "analytics",
                "Sin analytics el portafolio no se puede optimizar ni priorizar qué propiedades necesitan atención.",
                "Ocupación, rentabilidad, tiempo en mercado y desempeño por zona.",
                ["properties", "contracts"]),
        ],
        [
            new("properties", "contracts",  "Propiedades registradas → Contratos pueden vincularse a inmuebles específicos con historial"),
            new("clients",    "contracts",  "Clientes en base → Contratos pueden emitirse con datos del cliente ya precargados"),
            new("clients",    "viewings",   "Clientes registrados → Visitas pueden agendar sin perder el historial del prospecto"),
            new("contracts",  "payments",   "Contratos firmados → Pagos pueden programarse y monitorearse automáticamente"),
            new("contracts",  "analytics",  "Contratos activos → Analytics puede calcular ocupación y rendimiento del portafolio"),
        ]
    );

    // ── Healthcare ─────────────────────────────────────────────────────────

    private static IndustryProfile HealthcareProfile() => new(
        [
            new("patients",    "Pacientes",             ["patient", "paciente", "person"], true, "critical", "core",
                "El expediente del paciente es el centro de cualquier sistema de salud.",
                "Base para citas, historial clínico, prescripciones y seguimiento.",
                []),

            new("appointments","Citas Médicas",         ["appointment", "cita", "agenda", "consult", "schedule"], true, "critical", "core",
                "Sin agenda digital las citas se manejan en papel con riesgo de doble-booking y olvidos.",
                "Control de disponibilidad médica, reducción de esperas y confirmaciones automáticas.",
                ["patients"]),

            new("records",     "Expediente Clínico",   ["record", "expedient", "histori", "medical", "clinico"], true, "critical", "core",
                "El expediente es legalmente obligatorio y clínicamente crítico para la continuidad del cuidado.",
                "Diagnósticos, tratamientos, alergias y evolución del paciente en un solo lugar.",
                ["patients", "appointments"]),

            new("doctors",     "Médicos y Especialistas",["doctor", "medico", "physician", "especialist"], true, "high", "core",
                "Sin registro de médicos no hay asignación de citas ni control de agenda por especialidad.",
                "Agenda por especialidad, carga de trabajo y desempeño clínico.",
                []),

            new("prescriptions","Prescripciones",       ["prescription", "receta", "medicament", "drug"], false, "high", "integration",
                "Sin prescripciones digitales el paciente sale con papel y el médico sin historial de medicación.",
                "Alerta de interacciones, historial de medicamentos y trazabilidad de prescripciones.",
                ["records"]),

            new("billing",     "Facturación Médica",   ["bill", "invoic", "factura", "insurance", "seguro", "pago"], false, "high", "core",
                "Sin facturación integrada los cobros son manuales y los seguros difíciles de tramitar.",
                "Control de pagos, aseguradoras y deudas de pacientes.",
                ["appointments"]),

            new("labs",        "Laboratorio y Estudios",["lab", "laboratorio", "exam", "examen", "study", "resultado"], false, "medium", "growth",
                "Sin registro de laboratorio los resultados llegan en papel y se extravían o no se asocian al expediente.",
                "Resultados vinculados al expediente, alertas y trazabilidad diagnóstica.",
                ["records"]),

            new("analytics",   "Analytics Clínico",    ["analytic", "report", "reporte", "estadis"], false, "medium", "analytics",
                "Sin analytics el director médico no puede identificar enfermedades frecuentes ni medir productividad.",
                "Epidemiología, tiempos de espera, rentabilidad por servicio y calidad de atención.",
                ["billing", "records"]),
        ],
        [
            new("patients",    "records",       "Pacientes registrados → Expediente clínico puede estructurarse con historial completo"),
            new("appointments","records",       "Citas agendadas → Expediente puede documentarse en el momento de la consulta"),
            new("records",     "prescriptions", "Expediente activo → Prescripciones pueden vincularse con alertas de alergias e interacciones"),
            new("appointments","billing",       "Citas completadas → Facturación puede generarse automáticamente por servicio prestado"),
            new("records",     "analytics",     "Expedientes históricos → Analytics puede calcular prevalencia de patologías y tendencias"),
        ]
    );

    // ── Education ──────────────────────────────────────────────────────────

    private static IndustryProfile EducationProfile() => new(
        [
            new("students",   "Estudiantes",            ["student", "estudiante", "alumno", "learner"], true, "critical", "core",
                "Sin registro de estudiantes no hay matriculación, calificaciones ni comunicación posible.",
                "Base de todo el sistema: matrícula, calificaciones y comunicación con familias.",
                []),

            new("courses",    "Cursos y Materias",      ["course", "curso", "materia", "class", "subject"], true, "critical", "core",
                "Sin estructura de cursos no hay contenido que gestionar ni calificaciones que registrar.",
                "Catálogo de materias, asignación de docentes y base para calificaciones.",
                []),

            new("enrollment", "Matrículas",             ["enroll", "matricul", "registr", "admission"], true, "high", "core",
                "Sin control de matrículas el cupo es manual y los pagos de inscripción no son trazables.",
                "Control de cupos, proceso de inscripción y vinculación estudiante-curso.",
                ["students", "courses"]),

            new("teachers",   "Docentes",               ["teacher", "docente", "profesor", "instructor"], true, "high", "core",
                "Sin registro de docentes no se puede asignar materias, medir carga ni pagar horas.",
                "Asignación de materias, horarios y métricas de desempeño docente.",
                []),

            new("grades",     "Calificaciones",         ["grade", "calificacion", "nota", "score", "evaluacion"], false, "high", "integration",
                "Sin sistema de calificaciones el proceso de evaluación es manual y sin trazabilidad.",
                "Boletines automáticos, promedios y alertas de estudiantes en riesgo.",
                ["students", "courses", "enrollment"]),

            new("attendance", "Asistencia",             ["attendan", "asistencia", "presence", "absent"], false, "high", "growth",
                "Sin control de asistencia el director no puede detectar abandono ni actuar a tiempo.",
                "Alertas de inasistencia, reportes por curso y cumplimiento de requisitos mínimos.",
                ["students", "courses"]),

            new("payments",   "Pagos y Colegiaturas",   ["payment", "pago", "colegiatura", "tuition", "factura"], false, "high", "core",
                "Sin control de pagos los adeudos se pierden y el flujo de caja es impredecible.",
                "Control de adeudos, recordatorios de pago y reportes financieros de la institución.",
                ["enrollment"]),

            new("analytics",  "Analytics Académico",   ["analytic", "report", "reporte", "dashb", "estadis"], false, "medium", "analytics",
                "Sin analytics la dirección no puede identificar materias con mayor reprobación ni medir retención.",
                "Rendimiento académico, retención estudiantil y eficiencia docente.",
                ["grades", "attendance"]),
        ],
        [
            new("students",   "enrollment",  "Estudiantes registrados → Matrículas pueden vincularse con historial académico completo"),
            new("courses",    "enrollment",  "Cursos definidos → Matrículas pueden controlar cupos y listas por grupo"),
            new("enrollment", "grades",      "Matrículas activas → Calificaciones pueden registrarse y asociarse a cada estudiante"),
            new("students",   "attendance",  "Estudiantes matriculados → Asistencia puede tomarse por lista y enviar alertas a padres"),
            new("grades",     "analytics",   "Calificaciones históricas → Analytics puede detectar materias críticas y estudiantes en riesgo"),
        ]
    );

    // ── General ────────────────────────────────────────────────────────────

    private static IndustryProfile GeneralProfile() => new(
        [
            new("core_entity","Módulo Principal / Entidad Core", ["product", "item", "entity", "service", "recurso", "catalog"], true, "critical", "core",
                "Todo SaaS necesita una entidad central que represente su valor principal.",
                "Define el núcleo del sistema del que dependen todos los demás módulos.",
                []),

            new("users",      "Gestión de Usuarios",   ["user", "usuario", "member", "account", "profile"], true, "critical", "core",
                "Sin gestión de usuarios no hay multi-tenancy, permisos ni personalización.",
                "Base para roles, permisos, auditoría y experiencias personalizadas.",
                []),

            new("billing",    "Facturación / Pagos",   ["bill", "subscript", "payment", "pago", "invoic", "stripe"], false, "high", "core",
                "Sin facturación el SaaS no genera ingresos trazables ni puede escalar comercialmente.",
                "Monetización, control de suscripciones y reportes financieros.",
                ["users"]),

            new("notifications","Notificaciones",       ["notif", "email", "alert", "reminder", "webhook"], false, "high", "integration",
                "Las notificaciones son el canal de comunicación con el usuario. Sin ellas el sistema es pasivo.",
                "Retención de usuarios, alertas operacionales y comunicación proactiva.",
                []),

            new("audit",      "Auditoría / Logs",      ["audit", "log", "histori", "trace", "activity"], false, "medium", "integration",
                "La auditoría es crítica para cumplimiento, debugging y confianza del cliente enterprise.",
                "Trazabilidad completa, resolución de disputas y cumplimiento regulatorio.",
                ["users"]),

            new("reports",    "Reportes y Analytics",  ["report", "analytic", "reporte", "dashb", "metric", "kpi"], false, "medium", "analytics",
                "Sin reportes los usuarios no pueden medir el valor que obtienen del sistema.",
                "Retención de clientes, upsell basado en datos y justificación del ROI.",
                ["core_entity"]),

            new("api",        "API / Integraciones",   ["api", "webhook", "integration", "integrac", "zapier"], false, "low", "growth",
                "Las integraciones con otros sistemas son el diferenciador para clientes enterprise.",
                "Ecosistema de integraciones, automatizaciones y partners tecnológicos.",
                ["core_entity", "users"]),
        ],
        [
            new("users",       "billing",       "Usuarios activos → Facturación puede asociarse a cuentas y controlar acceso por plan"),
            new("core_entity", "reports",       "Entidad core con datos → Reportes pueden generar métricas de uso y valor entregado"),
            new("users",       "audit",         "Usuarios con acciones → Auditoría puede trazar cada cambio con responsable"),
            new("core_entity", "api",           "Módulo core definido → API puede exponerse para integraciones y automatizaciones"),
            new("billing",     "reports",       "Facturación activa → Reportes pueden mostrar MRR, churn y proyecciones de ingresos"),
        ]
    );
}
