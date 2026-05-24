using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuilderPlatform.Infrastructure.Services;

// ── Domain records ──────────────────────────────────────────────────────────

public record OwnershipGap(
    string Area,
    string Description,
    string Risk,
    string Severity,        // "critical" | "high" | "medium"
    string SuggestedOwner   // practical role title, not corporate jargon
);

public record HumanBottleneck(
    string Title,
    string Description,
    string Concentration,   // what's concentrated ("Pedidos + Cocina + Caja")
    string CollapseRisk,    // what happens when this person is unavailable
    string Severity
);

public record RoleSuggestion(
    string RoleTitle,
    string Responsibilities,
    string Priority,        // "immediate" | "soon" | "later"
    string BusinessCase
);

public record DelegationOpportunity(
    string Title,
    string CurrentState,
    string DelegationPath,
    string Impact
);

public record OrgReport(
    string                      ProductId,
    string                      Industry,
    string                      IndustryLabel,
    int                         OrgMaturityScore,
    string                      OrgMaturityTier,
    string                      OrgMaturityLabel,
    string                      OrgNarrative,
    string                      TopConcernTitle,
    string                      TopConcernDescription,
    List<OwnershipGap>          OwnershipGaps,
    List<HumanBottleneck>       HumanBottlenecks,
    List<RoleSuggestion>        RoleSuggestions,
    List<DelegationOpportunity> TopDelegationOpportunities,
    DateTime                    AnalyzedAt
);

// ── Profile sealed types ─────────────────────────────────────────────────────

sealed record OwnershipRule(string[] OperationKeywords, string[] ManagementKeywords, OwnershipGap Result);
sealed record BottleneckRule(string[] Group1, string[] Group2, string[] SafeguardKeywords, HumanBottleneck Result);
sealed record RoleRule(string[] TriggerKeywords, string[] AlreadyCoveredKeywords, RoleSuggestion Result);
sealed record DelegRule(string[] TriggerKeywords, string[] AlreadyDelegatedKeywords, DelegationOpportunity Result);
sealed record OwnedAreaDef(string[] OperationKeywords, string[] ManagementKeywords);

sealed record OrgProfile(
    string         IndustryKey,
    string         IndustryLabel,
    OwnedAreaDef[] ExpectedOwnershipAreas,
    OwnershipRule[] OwnershipRules,
    BottleneckRule[] BottleneckRules,
    RoleRule[]     RoleRules,
    DelegRule[]    DelegRules
);

// ── Engine ───────────────────────────────────────────────────────────────────

public class ProductOrgIntelligenceEngine
{
    private static readonly OrgProfile[] Profiles = BuildProfiles();

    public async Task<OrgReport> AnalyzeAsync(Guid productId, AppDbContext db, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Modules)
            .Include(p => p.Memory)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null) return Empty(productId.ToString());

        var industry = product.Memory.FirstOrDefault(m => m.Key == "industry")?.Value ?? "general";
        var profile  = Profiles.FirstOrDefault(p => p.IndustryKey == industry) ?? Profiles[^1];

        var moduleNames = product.Modules.Select(m => m.ModuleName).ToList();
        bool Has(string[] keywords) => keywords.Any(k =>
            moduleNames.Any(n => n.Contains(k, StringComparison.OrdinalIgnoreCase)));

        // Ownership coverage — how many expected areas have management coverage
        int totalAreas    = profile.ExpectedOwnershipAreas.Length;
        int coveredAreas  = totalAreas == 0 ? 0
            : profile.ExpectedOwnershipAreas
                .Count(a => Has(a.OperationKeywords) && Has(a.ManagementKeywords));
        int ownershipCoverage = totalAreas == 0 ? 50 : coveredAreas * 100 / totalAreas;

        // Ownership gaps — operations without management
        var gaps = profile.OwnershipRules
            .Where(r => Has(r.OperationKeywords) && !Has(r.ManagementKeywords))
            .Select(r => r.Result)
            .OrderByDescending(g => g.Severity == "critical" ? 2 : g.Severity == "high" ? 1 : 0)
            .Take(6)
            .ToList();

        // Human bottlenecks — multiple critical areas with no separation
        var bottlenecks = profile.BottleneckRules
            .Where(r => Has(r.Group1) && Has(r.Group2) && !Has(r.SafeguardKeywords))
            .Select(r => r.Result)
            .Take(3)
            .ToList();

        // Role suggestions — what roles this operation needs
        var roles = profile.RoleRules
            .Where(r => Has(r.TriggerKeywords) && !Has(r.AlreadyCoveredKeywords))
            .Select(r => r.Result)
            .Take(4)
            .ToList();

        // Delegation opportunities
        var delegations = profile.DelegRules
            .Where(r => Has(r.TriggerKeywords) && !Has(r.AlreadyDelegatedKeywords))
            .Select(r => r.Result)
            .Take(4)
            .ToList();

        // If no modules, show starter roles
        if (product.Modules.Count == 0)
        {
            roles       = profile.RoleRules.Take(2).Select(r => r.Result).ToList();
            delegations = [];
        }

        // Score
        int critGaps    = gaps.Count(g => g.Severity == "critical");
        int highGaps    = gaps.Count(g => g.Severity == "high");
        int medGaps     = gaps.Count(g => g.Severity == "medium");
        int gapPenalty  = Math.Min(60, critGaps * 22 + highGaps * 12 + medGaps * 5);
        int bnPenalty   = Math.Min(20, bottlenecks.Count * 12);
        int score       = Math.Max(0, Math.Min(100, ownershipCoverage - gapPenalty - bnPenalty));

        string tier = score >= 75 ? "scalable"
                    : score >= 50 ? "structured"
                    : score >= 20 ? "partially-delegated"
                    : "founder-operated";

        string tierLabel = tier switch
        {
            "scalable"             => "Organización Escalable",
            "structured"           => "Operación Estructurada",
            "partially-delegated"  => "Delegación Parcial",
            _                      => "Operado por el Fundador"
        };

        string narrative = BuildNarrative(tier, profile.IndustryLabel, gaps, bottlenecks, product.Modules.Count);
        var topConcern   = gaps.FirstOrDefault() ?? new OwnershipGap(
            "Sin módulos operacionales", "No hay módulos para analizar estructura organizacional.",
            "Agrega módulos para detectar gaps de ownership.", "medium", "Fundador/Dueño");

        return new OrgReport(
            productId.ToString(), profile.IndustryKey, profile.IndustryLabel,
            score, tier, tierLabel, narrative,
            topConcern.Area, topConcern.Risk,
            gaps, bottlenecks, roles, delegations, DateTime.UtcNow
        );
    }

    private static string BuildNarrative(string tier, string label, List<OwnershipGap> gaps, List<HumanBottleneck> bns, int moduleCount)
    {
        if (moduleCount == 0)
            return $"El sistema {label} está en fase inicial. Sin módulos, toda la operación depende de una sola persona que coordina todo manualmente.";

        return tier switch
        {
            "scalable"            => $"La operación {label} tiene ownership distribuido en áreas clave. Los procesos críticos tienen responsables identificados y la organización puede crecer sin depender de una sola persona.",
            "structured"          => $"El sistema {label} tiene estructura básica en las áreas principales, pero quedan {gaps.Count} área(s) sin ownership claro que generan dependencia operacional.",
            "partially-delegated" => $"La operación {label} está parcialmente delegada. Con {gaps.Count} gap(s) de ownership y {bns.Count} cuello(s) humano(s), hay áreas críticas donde todo pasa por una persona sin respaldo.",
            _                     => $"La operación {label} está fundador-operada: una persona centraliza la mayoría de decisiones operacionales. Con {gaps.Count} área(s) sin responsable claro, el crecimiento requerirá delegar antes de escalar."
        };
    }

    private static OrgReport Empty(string productId) =>
        new(productId, "general", "General", 0, "founder-operated", "Operado por el Fundador",
            "El sistema está en fase inicial. Toda la operación depende del fundador o una persona.",
            "Sin módulos registrados", "Agrega módulos para detectar gaps de ownership organizacional.",
            [], [], [], [], DateTime.UtcNow);

    // ── Industry profiles ─────────────────────────────────────────────────────

    private static OrgProfile[] BuildProfiles() =>
    [
        // ── Restaurant ────────────────────────────────────────────────────────
        new("restaurant", "Restaurante",
            ExpectedOwnershipAreas:
            [
                new(["pedido","orden","comanda"], ["supervisión","control","aprobación","gestión"]),
                new(["cocina","kitchen"],          ["supervisión","control","gestión","encargado"]),
                new(["inventario","stock"],        ["compras","control","gestión","responsable"]),
                new(["caja","venta"],              ["administración","control","cierre","reporte"]),
                new(["personal","staff"],          ["planilla","rrhh","recursos","coordinación"]),
            ],
            OwnershipRules:
            [
                new(["cocina","kitchen"], ["supervisión","control","gestión","encargado"],
                    new("Cocina sin Encargado Operacional",
                        "La producción de cocina no tiene un responsable formal de calidad, ritmo y coordinación de cocineros.",
                        "Sin encargado de cocina, el ritmo de producción y calidad dependen de la motivación individual. En horas pico, sin quien priorice y coordine, los tiempos colapsan.",
                        "critical", "Jefe de Cocina / Encargado de Producción")),
                new(["pedido","orden","comanda"], ["supervisión","control","aprobación","gestión"],
                    new("Operación de Sala sin Supervisión",
                        "El flujo de pedidos no tiene un responsable de validación, errores y experiencia del cliente.",
                        "Sin supervisión de sala, los errores de pedido (ingredientes incorrectos, tiempos de espera excesivos) se detectan tarde y afectan la experiencia del cliente.",
                        "critical", "Encargado de Sala / Capitán de Meseros")),
                new(["inventario","stock"], ["compras","aprobación","control","gestión"],
                    new("Inventario sin Responsable de Abastecimiento",
                        "Las decisiones de cuándo y qué comprar no tienen un responsable con autoridad formal.",
                        "Sin responsable de compras, las decisiones de reposición son reactivas. El dueño o cualquiera puede hacer compras sin criterio, generando descontrol de costos.",
                        "high", "Responsable de Abastecimiento")),
                new(["caja","venta"], ["administración","control","cierre","reporte"],
                    new("Caja sin Control Administrativo",
                        "El movimiento de dinero no tiene un responsable formal de cuadre, control y reportes.",
                        "Sin control de caja, los faltantes, errores de cobro y descuentos no autorizados son difíciles de detectar y corregir.",
                        "high", "Cajero Responsable / Administrador")),
                new(["personal","staff"], ["planilla","rrhh","recursos","horario"],
                    new("Personal sin Responsable de RRHH",
                        "La gestión de horarios, conflictos y ausentismo del equipo no tiene un responsable claro.",
                        "Sin quien gestione al personal, los conflictos de horarios, ausentismos y solicitudes van directo al dueño, consumiendo tiempo de gestión estratégica.",
                        "medium", "Coordinador de Personal / Encargado de Turno")),
            ],
            BottleneckRules:
            [
                new(["pedido","orden"], ["cocina","kitchen"], ["supervisión","gestión","control"],
                    new("Pedidos y Cocina Coordinados por Una Sola Persona",
                        "El mismo individuo (dueño o encargado general) recibe pedidos, coordina con cocina y atiende problemas de servicio.",
                        "Pedidos → Coordinación Cocina → Atención de Problemas",
                        "Si esta persona falta o se satura, el servicio se fragmenta: meseros sin dirección, cocina sin prioridades, clientes insatisfechos.",
                        "critical")),
                new(["caja","venta"], ["inventario","stock"], ["administración","control"],
                    new("Administración Centralizada: Caja e Inventario sin Separación",
                        "La misma persona maneja el dinero y el inventario sin separación de funciones.",
                        "Caja → Inventario → Compras",
                        "Sin separación de funciones, no hay control cruzado. Los errores o irregularidades en caja o inventario son imposibles de detectar.",
                        "high")),
            ],
            RoleRules:
            [
                new(["cocina","kitchen","pedido","orden"], ["supervisión","encargado","gestión"],
                    new("Encargado General de Operaciones",
                        "Coordina sala y cocina durante el servicio. Prioriza pedidos, resuelve conflictos y mantiene el ritmo de servicio.",
                        "immediate",
                        "Con pedidos y cocina activos, se necesita alguien con autoridad de coordinación en tiempo real.")),
                new(["inventario","stock"], ["administración","compras","control"],
                    new("Responsable de Abastecimiento",
                        "Gestiona el inventario, decide cuándo reponer, negocia con proveedores y controla costos de insumos.",
                        "soon",
                        "Con inventario activo, las decisiones de compra necesitan un responsable con criterio de costo-beneficio.")),
                new(["personal","staff","planilla"], ["rrhh","coordinación"],
                    new("Coordinador de Personal",
                        "Administra horarios, maneja ausencias, coordina turnos y es el primer punto de contacto para el equipo.",
                        "soon",
                        "Con personal y planilla activos, se necesita alguien que libere al dueño de la gestión operativa del equipo.")),
                new(["caja","venta","reporte"], ["administración","control"],
                    new("Administrador del Local",
                        "Responsable de cierre de caja, reportes financieros diarios y control de costos operativos.",
                        "later",
                        "Con operación de caja activa, el control financiero necesita un responsable que no sea el mismo que opera.")),
            ],
            DelegRules:
            [
                new(["cocina","kitchen"], ["supervisión","encargado","jefe"],
                    new("Delegar Coordinación de Cocina a Encargado Dedicado",
                        "El dueño o gerente coordina directamente con cocineros durante el servicio.",
                        "Designar un jefe de cocina con autoridad para priorizar pedidos, asignar tareas y mantener calidad. Liberar al dueño de la coordinación en tiempo real.",
                        "Dueño disponible para atender clientes VIP, problemas de servicio y decisiones estratégicas en vez de coordinar cocina.")),
                new(["inventario"], ["compras","responsable"],
                    new("Delegar Decisiones de Compra",
                        "El dueño decide qué, cuánto y cuándo comprar reaccionando al stock disponible.",
                        "Establecer par de stock mínimo y máximo por insumo, designar responsable de compras con autoridad hasta un monto definido.",
                        "Reposición proactiva y oportuna sin consumir tiempo de decisión del dueño.")),
                new(["personal","staff"], ["horario","turno"],
                    new("Delegar Gestión de Horarios a Encargado de Turno",
                        "El dueño aprueba cada cambio de turno, ausencia y conflicto de horario del equipo.",
                        "Designar un encargado de turno con autoridad para aprobar cambios de horario dentro de criterios predefinidos.",
                        "Libera al dueño de decisiones operativas de personal de bajo nivel.")),
            ]
        ),

        // ── HR / Payroll ───────────────────────────────────────────────────────
        new("hr_payroll", "RRHH y Planilla",
            ExpectedOwnershipAreas:
            [
                new(["asistencia","attendance"],  ["validación","control","aprobación","supervisión"]),
                new(["planilla","nómina"],         ["aprobación","validación","auditoría","control"]),
                new(["vacaciones","permiso"],      ["aprobación","gestión","control"]),
                new(["empleado","employee"],       ["administración","gestión","onboarding"]),
                new(["auditoría","audit"],         ["responsable","control","supervisión"]),
            ],
            OwnershipRules:
            [
                new(["planilla","nómina"], ["aprobación","validación","auditoría","control"],
                    new("Planilla sin Validador Responsable",
                        "El cálculo de nómina se genera sin un proceso formal de validación y aprobación por un responsable.",
                        "Sin validador, los errores de cálculo (horas mal contadas, deducciones incorrectas, extras no aprobados) se pagan antes de ser detectados, generando disputas y costos.",
                        "critical", "Coordinador de RRHH / Validador de Planilla")),
                new(["vacaciones","permiso","ausencia"], ["aprobación","gestión","control","flujo"],
                    new("Vacaciones sin Flujo de Aprobación Formal",
                        "Las solicitudes de vacaciones y permisos no pasan por un proceso de aprobación estructurado con responsable definido.",
                        "Sin aprobación formal, las ausencias se convierten en conflictos: el empleado asume que fue aprobado, el jefe no tiene registro. Genera confusión en planilla y conflictos de equipo.",
                        "critical", "Gerente de RRHH / Aprobador de Ausencias")),
                new(["empleado","employee"], ["gestión","administración","onboarding","incorporación"],
                    new("Empleados sin Responsable de Ciclo de Vida",
                        "La alta, baja y cambios de empleados no tienen un responsable formal que asegure que todos los pasos se completen.",
                        "Sin responsable de ciclo de vida, los empleados entran y salen sin documentación completa, generando riesgos laborales y legales.",
                        "high", "Administrador de RRHH")),
                new(["asistencia","attendance"], ["validación","control","supervisión","aprobación"],
                    new("Asistencia sin Validación de Supervisor",
                        "Los registros de asistencia se toman sin que un supervisor los valide antes de entrar a planilla.",
                        "Sin validación, un empleado puede registrar asistencia sin estar presente, o errores de marcador se trasladan directamente a nómina.",
                        "high", "Supervisor de Área / Validador de Asistencia")),
            ],
            BottleneckRules:
            [
                new(["asistencia","planilla"], ["vacaciones","permiso"], ["validación","control","supervisión"],
                    new("Toda la Gestión de Personal Centralizada en Una Persona",
                        "Asistencia, planilla y permisos son probablemente administrados por la misma persona sin separación de funciones.",
                        "Asistencia → Planilla → Permisos",
                        "Si la persona de RRHH falta, el ciclo de nómina completo se detiene. No hay nadie con conocimiento para reemplazarla temporalmente.",
                        "critical")),
                new(["empleado"], ["planilla","nómina"], ["auditoría","control"],
                    new("Datos de Empleados y Nómina sin Separación de Control",
                        "La misma persona que administra datos de empleados también calcula la planilla sin control cruzado.",
                        "Administración → Nómina",
                        "Sin separación, es imposible detectar irregularidades como salarios modificados, empleados fantasma o bonificaciones no autorizadas.",
                        "high")),
            ],
            RoleRules:
            [
                new(["planilla","nómina","asistencia"], ["validación","aprobación"],
                    new("Coordinador de RRHH",
                        "Valida asistencia, aprueba planilla, procesa vacaciones y es el primer punto de contacto de los empleados para temas administrativos.",
                        "immediate",
                        "Con planilla y asistencia activos, se necesita alguien que valide antes de pagar y que centralice la gestión de personal.")),
                new(["empleado","vacaciones"], ["gestión","aprobación"],
                    new("Gerente / Aprobador de RRHH",
                        "Aprueba vacaciones, cambios de cargo, ajustes salariales y resuelve conflictos de personal que escalen del coordinador.",
                        "soon",
                        "Con gestión de empleados y vacaciones, se necesita un nivel de aprobación con autoridad para decisiones que impactan la nómina.")),
                new(["auditoría","planilla"], ["control","responsable"],
                    new("Auditor Interno de Nómina",
                        "Revisa mensualmente la consistencia entre asistencia, contratos y pago. Detecta anomalías y genera reportes de compliance.",
                        "later",
                        "Con operación de planilla activa, se necesita alguien que valide la integridad del proceso, especialmente antes de inspecciones laborales.")),
            ],
            DelegRules:
            [
                new(["asistencia","planilla"], ["validación"],
                    new("Delegar Validación de Asistencia a Supervisores de Área",
                        "RRHH o el dueño valida toda la asistencia directamente.",
                        "Designar supervisores de área con autoridad para validar la asistencia de su equipo antes de que pase a nómina.",
                        "Validación distribuida y más precisa: el supervisor conoce mejor las ausencias reales de su equipo.")),
                new(["vacaciones","permiso"], ["aprobación","flujo"],
                    new("Delegar Aprobación de Ausencias a Jefes de Área",
                        "Todas las solicitudes de vacaciones pasan por RRHH central o el dueño.",
                        "Establecer que jefes de área aprueben vacaciones con menos de X días. Solo ausencias largas o sensibles van a RRHH.",
                        "Aprobaciones más rápidas, menos carga para RRHH central y empleados con respuesta inmediata.")),
            ]
        ),

        // ── Veterinary ────────────────────────────────────────────────────────
        new("veterinary", "Veterinaria",
            ExpectedOwnershipAreas:
            [
                new(["cita","turno","appointment"], ["recepción","gestión","coordinación"]),
                new(["paciente","mascota"],          ["historial","responsable","médico"]),
                new(["medicamento","medicina"],      ["control","inventario","farmacia"]),
                new(["factura","cobro","pago"],      ["administración","control","caja"]),
                new(["diagnóstico","tratamiento"],   ["médico","responsable","protocolo"]),
            ],
            OwnershipRules:
            [
                new(["paciente","mascota"], ["historial","responsable","médico","protocolo"],
                    new("Pacientes sin Médico Responsable Asignado",
                        "Los pacientes no tienen un médico responsable formal asignado en el sistema.",
                        "Sin médico responsable, cualquier otro veterinario que atienda no tiene claridad sobre el historial ni las decisiones tomadas. El continuity of care es riesgoso.",
                        "critical", "Médico Veterinario Responsable / Jefe Clínico")),
                new(["cita","turno","appointment"], ["recepción","gestión","coordinación","supervisor"],
                    new("Agenda de Citas sin Responsable de Recepción",
                        "El agendamiento de citas no tiene un responsable formal de coordinación y priorización.",
                        "Sin recepcionista responsable, las citas se cruzan, los tiempos de espera se extienden y los pacientes urgentes no reciben prioridad.",
                        "high", "Recepcionista / Coordinador de Agenda")),
                new(["medicamento","medicina"], ["control","inventario","responsable","farmacia"],
                    new("Medicamentos sin Responsable de Farmacia",
                        "El inventario de medicamentos no tiene un responsable de control, despacho y reposición.",
                        "Sin responsable de farmacia, los medicamentos se usan sin registro formal, generando desabasto sorpresivo y riesgo de errores de dosificación.",
                        "high", "Responsable de Farmacia / Paramédico")),
                new(["factura","cobro","pago"], ["administración","control","caja"],
                    new("Cobros sin Control Administrativo",
                        "La facturación y cobros no tienen un responsable de revisión y control.",
                        "Sin control de cobros, los descuentos no autorizados, servicios no cobrados y errores en facturas son difíciles de detectar.",
                        "medium", "Administrador / Cajero Responsable")),
            ],
            BottleneckRules:
            [
                new(["cita","turno"], ["paciente","mascota"], ["recepción","coordinación","supervisión"],
                    new("Agenda y Atención de Pacientes sin Separación de Roles",
                        "El médico veterinario probablemente gestiona su propia agenda y atiende a los pacientes sin apoyo de recepción.",
                        "Agenda → Atención → Seguimiento",
                        "Sin recepcionista, el médico pierde tiempo en tareas administrativas. Cuando está en consulta, nadie atiende el teléfono ni coordina la sala de espera.",
                        "critical")),
            ],
            RoleRules:
            [
                new(["cita","paciente","mascota"], ["recepción","coordinación"],
                    new("Recepcionista / Coordinador de Clínica",
                        "Agenda citas, gestiona sala de espera, coordina con médicos y atiende consultas por teléfono/WhatsApp.",
                        "immediate",
                        "Con citas y pacientes activos, se necesita separar la coordinación administrativa de la atención médica.")),
                new(["medicamento","medicina"], ["farmacia","control"],
                    new("Responsable de Farmacia y Suministros",
                        "Controla el inventario de medicamentos, registra despachos, gestiona reposiciones y reporta quiebres.",
                        "soon",
                        "Con medicamentos activos, alguien debe garantizar que el stock esté disponible y correctamente registrado.")),
                new(["diagnóstico","tratamiento","paciente"], ["jefe","responsable"],
                    new("Médico Jefe / Director Clínico",
                        "Define protocolos clínicos, supervisa casos complejos, asegura la calidad de diagnósticos y mentores al equipo médico.",
                        "later",
                        "Con equipo médico creciendo, se necesita alguien que mantenga estándares clínicos y resuelva casos difíciles.")),
            ],
            DelegRules:
            [
                new(["cita","turno"], ["recepción"],
                    new("Delegar Coordinación de Agenda a Recepcionista",
                        "El médico gestiona su propia agenda y confirma citas directamente.",
                        "Designar recepcionista con autoridad para agendar, reprogramar y confirmar citas según los criterios de prioridad definidos por el médico.",
                        "El médico se enfoca en la atención clínica. La sala de espera fluye mejor y los tiempos de espera se reducen.")),
                new(["medicamento"], ["control","responsable"],
                    new("Delegar Control de Farmacia",
                        "El médico o cualquier asistente despacha medicamentos sin registro formal.",
                        "Designar un responsable de farmacia que registre cada despacho, lleve inventario y genere alertas de stock mínimo.",
                        "Trazabilidad completa de medicamentos y eliminación de quiebres de stock sorpresivos.")),
            ]
        ),

        // ── E-Commerce ────────────────────────────────────────────────────────
        new("ecommerce", "E-Commerce",
            ExpectedOwnershipAreas:
            [
                new(["orden","pedido"],      ["fulfillment","operaciones","logística","control"]),
                new(["cliente","usuario"],   ["soporte","atención","servicio"]),
                new(["inventario","stock"],  ["control","administración","logística"]),
                new(["producto","catálogo"], ["gestión","administración","contenido"]),
                new(["devolución"],          ["gestión","aprobación","control"]),
            ],
            OwnershipRules:
            [
                new(["orden","pedido","compra"], ["fulfillment","operaciones","control","gestión"],
                    new("Órdenes sin Responsable de Fulfillment",
                        "El procesamiento, empaque y despacho de órdenes no tiene un responsable formal de operaciones.",
                        "Sin responsable de fulfillment, los errores de empaque, órdenes perdidas y retrasos de envío son frecuentes y se descubren cuando el cliente reclama.",
                        "critical", "Coordinador de Operaciones / Fulfillment Manager")),
                new(["cliente","usuario"], ["soporte","atención","servicio","tickets"],
                    new("Clientes sin Responsable de Soporte",
                        "Las consultas, reclamos y problemas de clientes no tienen un responsable formal de atención.",
                        "Sin soporte estructurado, el dueño o cualquier persona del equipo responde directamente, generando inconsistencias, tiempos de respuesta variables y pérdida de información.",
                        "critical", "Agente de Soporte / Gerente de Atención al Cliente")),
                new(["inventario","stock"], ["control","administración","responsable"],
                    new("Inventario sin Control de Calidad",
                        "El inventario no tiene un responsable que valide entradas, salidas y discrepancias.",
                        "Sin control de inventario, el overselling, los daños no detectados y las discrepancias son frecuentes.",
                        "high", "Encargado de Inventario")),
                new(["devolución","reembolso"], ["gestión","aprobación","proceso"],
                    new("Devoluciones sin Proceso de Aprobación Definido",
                        "Las devoluciones y reembolsos no tienen un responsable con criterios claros de aprobación.",
                        "Sin proceso formal, cada caso de devolución es una negociación, generando inconsistencias, clientes frustrados y costos difíciles de controlar.",
                        "medium", "Responsable de Postventa / Operaciones")),
            ],
            BottleneckRules:
            [
                new(["orden","pedido"], ["inventario","stock"], ["fulfillment","operaciones"],
                    new("Órdenes e Inventario sin Separación Operacional",
                        "La misma persona procesa las órdenes, controla el inventario y gestiona envíos.",
                        "Órdenes → Inventario → Envíos",
                        "Con volumen, esta persona se convierte en un cuello de botella. Un día de ausencia detiene toda la cadena de fulfillment.",
                        "critical")),
            ],
            RoleRules:
            [
                new(["orden","pedido","inventario"], ["fulfillment","operaciones"],
                    new("Coordinador de Operaciones / Fulfillment",
                        "Procesa órdenes, coordina inventario, genera guías de envío y hace seguimiento de entregas.",
                        "immediate",
                        "Con órdenes e inventario activos, se necesita alguien que sea dueño de toda la cadena de fulfillment.")),
                new(["cliente","usuario"], ["soporte","atención"],
                    new("Agente de Soporte al Cliente",
                        "Atiende consultas pre-venta, gestiona reclamos, procesa devoluciones y escala casos complejos.",
                        "soon",
                        "Con base de clientes creciente, el soporte necesita un responsable dedicado para mantener NPS.")),
                new(["producto","catálogo"], ["gestión","contenido"],
                    new("Responsable de Catálogo / Merchandising",
                        "Gestiona el catálogo de productos, actualiza precios, sube contenido y analiza qué vende bien.",
                        "later",
                        "Con catálogo amplio, se necesita alguien que optimice la presentación de productos y la experiencia de compra.")),
            ],
            DelegRules:
            [
                new(["orden","pedido"], ["fulfillment"],
                    new("Delegar Fulfillment a Operaciones Dedicadas",
                        "El dueño o asistente procesa cada orden manualmente.",
                        "Establecer un responsable de fulfillment con SOP claro: cómo procesar, empacar y despachar cada tipo de orden.",
                        "Mayor velocidad de procesamiento y menos errores. El dueño puede enfocarse en crecimiento.")),
                new(["cliente"], ["soporte","atención"],
                    new("Delegar Soporte de Primer Nivel",
                        "El dueño responde personalmente todas las consultas y reclamos de clientes.",
                        "Crear guía de respuestas para las consultas más frecuentes. Designar a alguien para soporte de primer nivel con escalación clara.",
                        "Tiempo de respuesta más rápido y dueño liberado para trabajo estratégico.")),
            ]
        ),

        // ── Logistics ─────────────────────────────────────────────────────────
        new("logistics", "Logística",
            ExpectedOwnershipAreas:
            [
                new(["ruta","entrega"],    ["coordinación","planificación","responsable"]),
                new(["conductor","driver"],["supervisión","control","gestión"]),
                new(["cliente"],           ["soporte","atención","seguimiento"]),
                new(["vehículo","flota"],  ["mantenimiento","control","responsable"]),
            ],
            OwnershipRules:
            [
                new(["ruta","entrega"], ["coordinación","planificación","responsable"],
                    new("Rutas sin Coordinador de Operaciones",
                        "La planificación y asignación de rutas no tiene un responsable formal de operaciones.",
                        "Sin coordinador, las rutas se asignan informalmente. Las incidencias (retrasos, entregas fallidas) no tienen un punto de escalación claro.",
                        "critical", "Coordinador de Operaciones Logísticas")),
                new(["conductor","driver"], ["supervisión","control","gestión"],
                    new("Conductores sin Supervisión Operacional",
                        "Los conductores no tienen un supervisor formal que los guíe en incidencias y valide el cumplimiento.",
                        "Sin supervisor, cada conductor decide cómo manejar las incidencias de entrega. No hay estándar de servicio ni control de calidad.",
                        "high", "Supervisor de Flota")),
                new(["vehículo","flota"], ["mantenimiento","control","responsable"],
                    new("Flota sin Responsable de Mantenimiento",
                        "El mantenimiento de vehículos no tiene un responsable que asegure el programa preventivo.",
                        "Sin responsable de mantenimiento, los vehículos se reparan reactivamente. Las fallas en ruta generan costos de emergencia.",
                        "high", "Responsable de Flota y Mantenimiento")),
            ],
            BottleneckRules:
            [
                new(["ruta","entrega"], ["conductor","driver"], ["coordinación","supervisión"],
                    new("Operación de Entregas Centralizada en Coordinación Manual",
                        "El mismo individuo planifica rutas, asigna conductores y gestiona incidencias.",
                        "Planificación → Asignación → Gestión de Incidencias",
                        "Con ausencia del coordinador, las entregas del día quedan sin planificación ni seguimiento.",
                        "critical")),
            ],
            RoleRules:
            [
                new(["ruta","conductor","entrega"], ["coordinación"],
                    new("Coordinador de Operaciones",
                        "Planifica rutas, asigna conductores, monitorea entregas y gestiona incidencias en tiempo real.",
                        "immediate",
                        "Con rutas y conductores activos, se necesita alguien dedicado a la coordinación operacional diaria.")),
                new(["vehículo","flota"], ["mantenimiento"],
                    new("Responsable de Flota",
                        "Programa mantenimientos preventivos, coordina reparaciones y lleva registro del estado de cada vehículo.",
                        "soon",
                        "Con flota activa, el mantenimiento necesita un responsable que evite fallas costosas en ruta.")),
            ],
            DelegRules:
            [
                new(["conductor","ruta"], ["coordinación"],
                    new("Delegar Coordinación Diaria a Despachador",
                        "El gerente asigna rutas y conductores directamente cada mañana.",
                        "Designar un despachador responsable de asignación diaria con criterios predefinidos de prioridad y capacidad.",
                        "El gerente puede enfocarse en planificación estratégica y relación con clientes.")),
            ]
        ),

        // ── Real Estate ───────────────────────────────────────────────────────
        new("real_estate", "Bienes Raíces",
            ExpectedOwnershipAreas:
            [
                new(["propiedad","inmueble"], ["responsable","agente","gestión"]),
                new(["cliente","prospecto"],  ["agente","asesor","coordinación"]),
                new(["contrato"],             ["revisión","aprobación","legal"]),
                new(["alquiler","renta"],     ["administración","control","gestión"]),
            ],
            OwnershipRules:
            [
                new(["propiedad","inmueble"], ["responsable","agente","gestión","administración"],
                    new("Propiedades sin Agente Responsable Asignado",
                        "Las propiedades no tienen un agente asignado formalmente responsable de su venta o alquiler.",
                        "Sin agente responsable, las propiedades quedan en limbo: nadie hace seguimiento activo, los interesados no reciben respuesta oportuna.",
                        "high", "Agente Inmobiliario Responsable")),
                new(["cliente","prospecto"], ["agente","asesor","coordinación","seguimiento"],
                    new("Prospectos sin Agente de Seguimiento",
                        "Los prospectos no tienen un asesor asignado formalmente responsable de dar seguimiento.",
                        "Sin asesor asignado, los prospectos reciben atención inconsistente. Muchos se pierden por falta de seguimiento oportuno.",
                        "critical", "Asesor Comercial / Agente")),
                new(["contrato"], ["revisión","aprobación","legal","validación"],
                    new("Contratos sin Revisión Formal",
                        "Los contratos se generan sin un proceso formal de revisión y aprobación.",
                        "Sin revisión formal, los contratos con cláusulas incorrectas, precios erróneos o condiciones desfavorables se firman sin detectarse.",
                        "high", "Revisor Legal / Gerente de Contratos")),
            ],
            BottleneckRules:
            [
                new(["propiedad","inmueble"], ["cliente","prospecto"], ["agente","coordinación"],
                    new("Propiedades y Prospectos Coordinados por Una Sola Persona",
                        "El dueño de la agencia gestiona tanto las propiedades como los prospectos sin delegar.",
                        "Propiedades → Prospectos → Cierres",
                        "Sin agentes dedicados, el dueño es el cuello de botella. Cada nueva propiedad o prospecto adicional reduce la calidad de atención de los existentes.",
                        "high")),
            ],
            RoleRules:
            [
                new(["cliente","prospecto","propiedad"], ["agente","asesor"],
                    new("Asesor Comercial / Agente Inmobiliario",
                        "Gestiona prospectos, coordina visitas, presenta propiedades y hace seguimiento hasta el cierre.",
                        "immediate",
                        "Con prospectos y propiedades activos, se necesita un agente dedicado por zona o tipo de propiedad.")),
                new(["contrato","alquiler"], ["administración"],
                    new("Administrador de Contratos y Alquileres",
                        "Gestiona contratos, cobros de alquiler, renovaciones y es el punto de contacto para propietarios e inquilinos.",
                        "soon",
                        "Con contratos y alquileres activos, la administración necesita un responsable dedicado.")),
            ],
            DelegRules:
            [
                new(["prospecto","cliente"], ["agente"],
                    new("Delegar Seguimiento de Prospectos a Agentes",
                        "El dueño hace seguimiento directo a todos los prospectos.",
                        "Asignar prospectos a agentes según zona y tipo. El dueño solo interviene en cierres de alta complejidad.",
                        "Mayor tasa de seguimiento, menos prospectos perdidos y dueño disponible para crecer la operación.")),
            ]
        ),

        // ── Healthcare ────────────────────────────────────────────────────────
        new("healthcare", "Salud",
            ExpectedOwnershipAreas:
            [
                new(["cita","consulta"],           ["recepción","coordinación","gestión"]),
                new(["paciente"],                   ["médico","responsable","protocolo"]),
                new(["diagnóstico","tratamiento"], ["médico","responsable","protocolo"]),
                new(["laboratorio","examen"],       ["técnico","responsable","control"]),
                new(["factura","cobro"],            ["administración","control","caja"]),
            ],
            OwnershipRules:
            [
                new(["paciente"], ["médico","responsable","protocolo","jefe"],
                    new("Pacientes sin Médico Responsable Asignado",
                        "Los pacientes no tienen un médico responsable formal de su atención continua.",
                        "Sin asignación formal, la continuidad de atención se rompe: diferente médico en cada visita, sin quien conozca el historial completo.",
                        "critical", "Médico de Cabecera / Responsable de Paciente")),
                new(["cita","consulta"], ["recepción","coordinación","gestión"],
                    new("Agenda Médica sin Coordinación de Recepción",
                        "La gestión de la agenda no tiene un responsable formal de coordinación.",
                        "Sin recepcionista responsable, las citas se cruzan, los tiempos de espera se extienden y los pacientes urgentes no reciben prioridad.",
                        "high", "Recepcionista / Coordinador de Agenda")),
                new(["diagnóstico","tratamiento"], ["protocolo","aprobación","supervisión"],
                    new("Tratamientos sin Protocolo de Supervisión",
                        "Los tratamientos se aplican sin un protocolo formal de supervisión y validación clínica.",
                        "Sin supervisión clínica, los desvíos de protocolo, errores de dosificación y casos atípicos no se detectan oportunamente.",
                        "high", "Director Médico / Coordinador Clínico")),
            ],
            BottleneckRules:
            [
                new(["cita","consulta"], ["paciente"], ["recepción","coordinación"],
                    new("Agenda y Atención Médica sin Separación de Roles",
                        "El médico gestiona su propia agenda además de atender pacientes.",
                        "Agenda → Atención → Seguimiento",
                        "Sin apoyo administrativo, el médico pierde tiempo clínico en tareas de coordinación y la calidad de atención disminuye.",
                        "critical")),
            ],
            RoleRules:
            [
                new(["cita","paciente"], ["recepción","coordinación"],
                    new("Recepcionista / Coordinador de Citas",
                        "Agenda citas, gestiona sala de espera, recibe pacientes y coordina con el equipo médico.",
                        "immediate",
                        "Con citas y pacientes activos, separar la coordinación administrativa de la atención médica es inmediato.")),
                new(["diagnóstico","tratamiento"], ["protocolo","supervisión"],
                    new("Director / Coordinador Médico",
                        "Establece protocolos clínicos, supervisa casos complejos y es el responsable de la calidad clínica.",
                        "soon",
                        "Con equipo médico activo, se necesita alguien que mantenga estándares y resuelva casos difíciles.")),
            ],
            DelegRules:
            [
                new(["cita","consulta"], ["recepción"],
                    new("Delegar Coordinación de Agenda",
                        "El médico confirma citas directamente con los pacientes.",
                        "Designar recepcionista con autoridad para agendar, confirmar y reprogramar citas según la disponibilidad del médico.",
                        "El médico puede enfocarse completamente en la atención clínica.")),
            ]
        ),

        // ── Education ─────────────────────────────────────────────────────────
        new("education", "Educación",
            ExpectedOwnershipAreas:
            [
                new(["estudiante","alumno"],   ["coordinación","gestión","responsable"]),
                new(["curso","clase"],         ["docente","responsable","coordinación"]),
                new(["calificación","nota"],   ["validación","aprobación","coordinación"]),
                new(["pago","mensualidad"],    ["administración","control","caja"]),
                new(["docente","profesor"],    ["coordinación","supervisión","gestión"]),
            ],
            OwnershipRules:
            [
                new(["curso","clase"], ["docente","responsable","coordinación"],
                    new("Cursos sin Docente Responsable Asignado",
                        "Los cursos no tienen un docente formalmente asignado como responsable de contenido y resultados.",
                        "Sin docente responsable, la calidad del contenido y los resultados de aprendizaje no tienen un responsable de cuenta.",
                        "high", "Docente Responsable / Coordinador Académico")),
                new(["estudiante","alumno"], ["coordinación","gestión","responsable","tutor"],
                    new("Estudiantes sin Coordinador de Seguimiento",
                        "Los estudiantes no tienen un responsable formal de seguimiento académico y bienestar.",
                        "Sin coordinador, los estudiantes con dificultades académicas o problemas personales no tienen un punto de contacto claro para solicitar ayuda.",
                        "high", "Coordinador Académico / Tutor")),
                new(["pago","mensualidad"], ["administración","control","caja","responsable"],
                    new("Pagos sin Control Administrativo",
                        "Los pagos de mensualidades y matrículas no tienen un responsable formal de control y seguimiento.",
                        "Sin control de pagos, la morosidad pasa desapercibida y el seguimiento de pagos pendientes consume tiempo del dueño o director.",
                        "medium", "Administrador Escolar / Encargado de Cobranza")),
                new(["calificación","nota"], ["validación","aprobación","coordinación"],
                    new("Calificaciones sin Validación Académica",
                        "Las notas se ingresan sin un proceso formal de revisión y aprobación por parte de coordinación.",
                        "Sin validación, errores de calificación (notas incorrectas, ausencias no registradas) llegan a los padres sin corrección previa.",
                        "medium", "Coordinador Académico / Director")),
            ],
            BottleneckRules:
            [
                new(["estudiante","alumno"], ["curso","clase"], ["coordinación","administración"],
                    new("Estudiantes y Cursos Coordinados sin Estructura Formal",
                        "La misma persona (dueño o director) coordina estudiantes, cursos y docentes sin delegación.",
                        "Estudiantes → Cursos → Docentes",
                        "Sin coordinadores, el director es el cuello de botella. Cada nueva queja, pedido o problema escala directamente.",
                        "high")),
            ],
            RoleRules:
            [
                new(["estudiante","curso"], ["coordinación"],
                    new("Coordinador Académico",
                        "Coordina el desempeño de los estudiantes, hace seguimiento a docentes y gestiona el calendario académico.",
                        "immediate",
                        "Con estudiantes y cursos activos, se necesita alguien que gestione el flujo académico sin saturar al director.")),
                new(["pago","mensualidad"], ["administración"],
                    new("Administrador Escolar / Encargado de Cobranza",
                        "Gestiona matrículas, mensualidades, emite recibos y hace seguimiento de morosidad.",
                        "soon",
                        "Con pagos activos, se necesita separar la administración financiera de la gestión académica.")),
            ],
            DelegRules:
            [
                new(["estudiante"], ["coordinación"],
                    new("Delegar Seguimiento Estudiantil a Coordinador",
                        "El director atiende directamente cada consulta de estudiantes y padres.",
                        "Designar coordinador académico con autoridad para resolver consultas académicas, coordinar con docentes y gestionar solicitudes de estudiantes.",
                        "Director disponible para planificación estratégica, captación y relaciones con el entorno.")),
                new(["pago","mensualidad"], ["administración"],
                    new("Delegar Cobranza a Administrador",
                        "El director gestiona directamente el seguimiento de pagos y morosidad.",
                        "Designar administrador escolar responsable de enviar estados de cuenta, hacer seguimiento de morosos y reportar al director solo los casos críticos.",
                        "Mejor tasa de cobro, proceso más profesional y director sin desgaste en gestión financiera operativa.")),
            ]
        ),

        // ── General ───────────────────────────────────────────────────────────
        new("general", "General",
            ExpectedOwnershipAreas:
            [
                new(["proceso","flujo","operación"], ["responsable","control","gestión"]),
                new(["cliente","usuario"],           ["soporte","atención","responsable"]),
                new(["dato","reporte","información"], ["análisis","control","responsable"]),
            ],
            OwnershipRules:
            [
                new(["proceso","flujo","operación"], ["responsable","control","gestión","supervisión"],
                    new("Procesos sin Responsables Claros",
                        "Los procesos principales del sistema no tienen un responsable formal asignado.",
                        "Sin ownership claro, los errores operacionales no tienen a quién escalar y las decisiones se toman ad-hoc por quien esté disponible.",
                        "high", "Responsable de Operaciones")),
                new(["cliente","usuario"], ["soporte","atención","responsable","servicio"],
                    new("Atención al Cliente sin Responsable",
                        "Las consultas y problemas de clientes no tienen un responsable formal de atención.",
                        "Sin responsable de atención, la experiencia del cliente es inconsistente y los problemas se resuelven con tiempos variables.",
                        "high", "Responsable de Atención al Cliente")),
                new(["dato","reporte","información"], ["análisis","control","responsable"],
                    new("Datos sin Responsable de Análisis",
                        "La información generada por el sistema no tiene un responsable de análisis y uso para toma de decisiones.",
                        "Sin analista, los datos se acumulan sin generar insights accionables para el equipo.",
                        "medium", "Analista de Operaciones / Responsable de Datos")),
            ],
            BottleneckRules:
            [
                new(["proceso","flujo"], ["cliente","usuario"], ["gestión","control"],
                    new("Operaciones y Atención al Cliente Centralizadas",
                        "La misma persona maneja los procesos internos y atiende a clientes directamente.",
                        "Procesos → Atención → Decisiones",
                        "Sin delegación, la persona clave es un punto único de fallo. Su ausencia detiene tanto la operación interna como la atención externa.",
                        "high")),
            ],
            RoleRules:
            [
                new(["proceso","flujo"], ["responsable","gestión"],
                    new("Responsable de Operaciones",
                        "Coordina los procesos principales, identifica cuellos de botella y escala problemas con criterio.",
                        "immediate",
                        "Con procesos activos, se necesita alguien que sea dueño de la operación diaria.")),
                new(["cliente","usuario"], ["soporte"],
                    new("Responsable de Atención al Cliente",
                        "Gestiona consultas, resuelve problemas y es el punto de contacto principal para los clientes.",
                        "soon",
                        "Con clientes y usuarios activos, se necesita separar la atención del cliente del trabajo operativo interno.")),
            ],
            DelegRules:
            [
                new(["proceso","flujo"], ["responsable"],
                    new("Delegar Operación Diaria a Responsable Designado",
                        "El fundador gestiona la operación diaria directamente.",
                        "Designar un responsable de operaciones con autoridad para tomar decisiones operativas dentro de parámetros definidos.",
                        "Fundador disponible para trabajo estratégico, crecimiento y relaciones clave.")),
            ]
        ),
    ];
}
