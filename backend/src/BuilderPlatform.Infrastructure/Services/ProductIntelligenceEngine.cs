using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuilderPlatform.Infrastructure.Services;

// ── Public DTOs ───────────────────────────────────────────────────────────────

public record IntelligenceGap(
    string Module,
    string Reason,
    string Priority,   // "high" | "medium" | "low"
    string Category    // "missing_module" | "missing_connection"
);

public record IntelligenceConnection(
    string From,
    string To,
    string Label,
    bool   Detected,
    string Impact
);

public record IntelligenceSuggestion(
    string Title,
    string Context,
    string Impact,
    string Category    // "operational" | "automation" | "reporting" | "financial"
);

public record ProactiveInsight(
    string Type,      // "critical_gap" | "missing_connection" | "evolution"
    string Severity,  // "high" | "medium" | "low"
    string Title,
    string Detail,
    string Action
);

public record IntelligenceReport(
    string                       ProductId,
    string                       Industry,
    string                       IndustryLabel,
    int                          ModuleCount,
    string                       EvolutionStage,
    string                       EvolutionStageLabel,
    string                       EvolutionNextMilestone,
    List<IntelligenceGap>        Gaps,
    List<IntelligenceConnection> Connections,
    List<IntelligenceSuggestion> Suggestions,
    string                       Narrative,
    DateTime                     AnalyzedAt,
    // ── Sprint 39: Proactive Intelligence ─────────────────────────────────────
    string                       HealthScore,        // "starter"|"operational"|"growing"|"mature"
    string                       HealthScoreLabel,   // "Inicial"|"Operacional"|"Creciendo"|"Maduro"
    int                          HealthScoreNumeric, // 0-100
    int                          CriticalCount,      // high-severity gap count
    List<ProactiveInsight>       TopInsights         // max 4 actionable items
);

// ── Engine ────────────────────────────────────────────────────────────────────

public class ProductIntelligenceEngine
{
    private sealed record IntelligenceProfile(
        string   IndustryLabel,
        string[] CoreModules,
        string[] GrowthModules,
        string[] AdvancedModules,
        (string[] A, string[] B, string Label, string Impact)[] ConnectionRules,
        (string[] Keywords, string Module, string Reason, string Priority)[] GapRules,
        string   StarterNarrative,
        string   GrowthNarrative,
        string   MatureNarrative,
        string   StarterMilestone,
        string   GrowthMilestone
    );

    private static readonly Dictionary<string, IntelligenceProfile> Profiles = new()
    {
        ["restaurant"] = new(
            IndustryLabel: "Restaurantes / F&B",
            CoreModules:   ["mesas", "pedidos", "menu", "dashboard", "order", "table"],
            GrowthModules: ["inventario", "cocina", "kds", "reservas", "compras", "inventory", "kitchen", "reservat"],
            AdvancedModules: ["reportes", "analytics", "crm", "facturacion", "pos"],
            ConnectionRules: [
                (["pedidos", "orden", "comanda", "order"], ["cocina", "kds", "kitchen"],
                 "Pedidos deben fluir automáticamente a pantalla de cocina (KDS)", "Operaciones críticas"),
                (["inventario", "stock", "inventory"], ["compras", "proveedor", "supplier", "purchas"],
                 "El inventario bajo debe generar órdenes de compra automáticas", "Control de costos"),
                (["mesas", "mesa", "table"], ["reservas", "reservacion", "reservat"],
                 "Las mesas deben integrarse con reservaciones para control de capacidad", "Gestión de capacidad"),
                (["pedidos", "ventas", "orden", "order"], ["reportes", "analytics"],
                 "Los pedidos alimentan el dashboard financiero con métricas de venta", "Visibilidad ejecutiva"),
            ],
            GapRules: [
                (["cocina", "kds", "kitchen"], "Cocina / KDS",
                 "No hay pantalla de cocina — los pedidos existen pero no tienen destino operacional", "high"),
                (["inventario", "stock", "inventory"], "Control de Inventario",
                 "Sin inventario no es posible controlar costos de materia prima ni detectar faltantes", "high"),
                (["compras", "proveedor", "purchas"], "Compras / Proveedores",
                 "No hay módulo de compras — el inventario no se puede reabastecer sistemáticamente", "medium"),
                (["reportes", "analytics"], "Reportes de Ventas",
                 "Sin reportes no hay visibilidad de tendencias, platos más vendidos o margen bruto", "medium"),
                (["reservas", "reservacion", "reservat"], "Reservaciones",
                 "Sin reservaciones la gestión de capacidad es completamente manual", "low"),
            ],
            StarterNarrative: "Este sistema de restaurante tiene la base operacional de mesas y pedidos. El flujo está incompleto: sin cocina y sin inventario, la operación diaria depende de procesos manuales.",
            GrowthNarrative:  "Buen avance operacional. Cocina e inventario activos. El siguiente ciclo es cerrar el financiero: reportes de ventas, control de compras y margen por plato.",
            MatureNarrative:  "Sistema operacional maduro. El siguiente nivel es analytics predictivo de ventas, CRM para fidelización y automatización de reabastecimiento.",
            StarterMilestone: "Agregar Cocina/KDS y conectar el flujo pedidos→cocina",
            GrowthMilestone:  "Integrar reportes de ventas y cerrar el ciclo inventario→compras"
        ),

        ["hr_payroll"] = new(
            IndustryLabel: "RRHH / Planilla",
            CoreModules:   ["empleados", "asistencia", "planilla", "dashboard"],
            GrowthModules: ["vacaciones", "permisos", "beneficios", "evaluaciones", "departamentos"],
            AdvancedModules: ["reclutamiento", "analytics", "portal", "formacion"],
            ConnectionRules: [
                (["asistencia", "attendance", "marcas"], ["planilla", "payroll", "quincena", "nomina"],
                 "La asistencia debería alimentar automáticamente el cálculo de planilla", "Automatización de nómina"),
                (["vacaciones", "ausencias", "permisos", "licencia"], ["planilla", "payroll", "quincena"],
                 "Las ausencias y vacaciones deben afectar el cálculo quincenal de forma directa", "Exactitud de planilla"),
                (["empleados", "personal", "staff"], ["asistencia", "attendance"],
                 "Cada empleado debe tener historial de asistencia individual trazable", "Control de personal"),
                (["departamentos", "areas", "departamento"], ["empleados", "personal"],
                 "La estructura departamental organiza la granularidad de reportes de planilla", "Estructura organizacional"),
            ],
            GapRules: [
                (["vacaciones", "permisos", "ausencias", "licencia"], "Vacaciones / Permisos",
                 "Sin módulo de vacaciones no hay registro formal de ausencias — riesgo legal en Costa Rica", "high"),
                (["reportes", "ccss", "caja", "patronal"], "Reportes CCSS / Legales",
                 "Los reportes para CCSS son obligación legal — su ausencia representa riesgo de cumplimiento", "high"),
                (["beneficios", "incentivos", "deducciones"], "Beneficios y Deducciones",
                 "Sin módulo de beneficios el cálculo de planilla es incompleto y propenso a errores", "medium"),
                (["evaluaciones", "desempenio", "performance"], "Evaluaciones de Desempeño",
                 "Sin evaluaciones no hay trazabilidad de productividad — brecha en gestión de talento", "medium"),
                (["departamentos", "departamento", "areas"], "Estructura Departamental",
                 "Sin departamentos los reportes de planilla no tienen granularidad organizacional", "low"),
            ],
            StarterNarrative: "El sistema tiene el núcleo de RRHH: empleados, asistencia y planilla. Sin vacaciones ni reportes legales, hay riesgo de cumplimiento con CCSS.",
            GrowthNarrative:  "Buena cobertura de RRHH. El siguiente paso crítico es automatizar la conexión asistencia→planilla y generar reportes para CCSS.",
            MatureNarrative:  "Sistema HR maduro. Considera evaluaciones de desempeño, portal de autoservicio para empleados y analytics de productividad.",
            StarterMilestone: "Agregar Vacaciones/Permisos y conectar Asistencia con el cálculo de Planilla",
            GrowthMilestone:  "Automatizar planilla desde asistencia y habilitar reportes CCSS"
        ),

        ["veterinary"] = new(
            IndustryLabel: "Veterinaria",
            CoreModules:   ["pacientes", "citas", "historial", "dashboard"],
            GrowthModules: ["medicamentos", "facturacion", "propietarios", "inventario"],
            AdvancedModules: ["laboratorio", "recordatorios", "analytics"],
            ConnectionRules: [
                (["citas", "agenda", "appointment"], ["historial", "records", "historia", "clinico"],
                 "Cada cita cerrada debería generar o actualizar el historial médico del paciente", "Trazabilidad clínica"),
                (["medicamentos", "tratamiento", "farmacia"], ["historial", "records"],
                 "Los medicamentos recetados deben quedar vinculados al historial del paciente", "Historial completo"),
                (["citas", "servicio"], ["facturacion", "billing", "cobro"],
                 "Las citas completadas deben generar factura automáticamente", "Flujo de facturación"),
                (["propietarios", "owner", "cliente", "duenio"], ["pacientes", "patient", "mascota"],
                 "Los propietarios deben estar vinculados explícitamente a sus mascotas", "Gestión de clientes"),
            ],
            GapRules: [
                (["medicamentos", "farmacia", "tratamiento"], "Medicamentos / Farmacia",
                 "Sin medicamentos los tratamientos no tienen trazabilidad clínica — riesgo para el paciente", "high"),
                (["facturacion", "billing", "cobro", "pago"], "Facturación",
                 "Sin facturación no hay flujo de cobro formal — pérdida de ingresos directa", "high"),
                (["propietarios", "owner", "cliente", "duenio"], "Propietarios / Clientes",
                 "Sin propietarios las mascotas no tienen contacto de responsabilidad registrado", "medium"),
                (["recordatorio", "notificacion"], "Recordatorios / Notificaciones",
                 "Sin recordatorios los clientes no reciben alertas de vacunas o citas próximas", "medium"),
            ],
            StarterNarrative: "El sistema veterinario cubre citas y pacientes. Sin medicamentos y facturación, el flujo clínico y financiero está incompleto.",
            GrowthNarrative:  "Buena cobertura clínica. El siguiente paso es cerrar el ciclo de cobro con facturación y automatizar recordatorios a propietarios.",
            MatureNarrative:  "Clínica bien cubierta. Considera agregar laboratorio, inventario de medicamentos y analytics de salud de pacientes.",
            StarterMilestone: "Agregar Medicamentos y Facturación para completar el flujo clínico básico",
            GrowthMilestone:  "Automatizar recordatorios de vacunas y vincular citas con historial médico"
        ),

        ["gaming"] = new(
            IndustryLabel: "Gaming / Casinos",
            CoreModules:   ["maquinas", "sucursales", "operadores", "dashboard"],
            GrowthModules: ["tareas", "alertas", "mantenimiento", "monitoreo"],
            AdvancedModules: ["reportes", "analytics", "rendimiento"],
            ConnectionRules: [
                (["maquinas", "machine", "slot"], ["alertas", "alerta", "alert"],
                 "Las máquinas deben generar alertas automáticas ante falla o baja disponibilidad", "Disponibilidad operacional"),
                (["tareas", "task", "trabajo"], ["operadores", "operator", "tecnico"],
                 "Las tareas de mantenimiento deben asignarse y trazarse por operador de campo", "Gestión de campo"),
                (["maquinas", "slot"], ["rendimiento", "performance", "metricas"],
                 "Cada máquina debe tener métricas de rendimiento e ingresos históricos", "Análisis de activos"),
                (["sucursales", "branch", "sucursal"], ["maquinas", "machine"],
                 "Las sucursales deben mostrar inventario completo de máquinas activas e inactivas", "Control de activos"),
            ],
            GapRules: [
                (["alertas", "alerta", "alert"], "Sistema de Alertas",
                 "Sin alertas las fallas de máquinas se detectan tarde — cada hora muerta es pérdida directa", "high"),
                (["reportes", "analytics", "metricas"], "Reportes de Rendimiento",
                 "Sin reportes no hay visibilidad de qué máquinas generan más revenue por sucursal", "high"),
                (["mantenimiento", "maintenance"], "Mantenimiento Preventivo",
                 "Sin mantenimiento preventivo la operación es completamente reactiva ante fallas", "medium"),
            ],
            StarterNarrative: "El sistema de gaming tiene máquinas y sucursales registradas. Sin alertas automáticas ni reportes de rendimiento, la operación es reactiva.",
            GrowthNarrative:  "Buen control de campo. El siguiente paso es agregar analytics de rendimiento por máquina y automatizar alertas de falla.",
            MatureNarrative:  "Sistema operacional maduro. Considera predicción de fallas y análisis de ROI por máquina y sucursal.",
            StarterMilestone: "Implementar alertas automáticas y asignación de tareas por operador",
            GrowthMilestone:  "Agregar reportes de rendimiento por sucursal y máquina con tendencias"
        ),

        ["real_estate"] = new(
            IndustryLabel: "Bienes Raíces",
            CoreModules:   ["propiedades", "clientes", "contratos", "dashboard"],
            GrowthModules: ["visitas", "documentos", "pagos", "reportes"],
            AdvancedModules: ["analytics", "crm", "notificaciones", "portal"],
            ConnectionRules: [
                (["propiedades", "inmueble", "propiedad"], ["visitas", "cita", "agenda"],
                 "Las propiedades deben tener agenda de visitas integrada para coordinación de showing", "Gestión de ventas"),
                (["clientes", "cliente", "prospecto"], ["contratos", "contrato"],
                 "Los clientes deben vincularse a sus contratos activos e históricos", "Trazabilidad comercial"),
                (["contratos", "contrato"], ["pagos", "pago", "cobro"],
                 "Los contratos deben generar y rastrear el flujo de pagos automáticamente", "Control financiero"),
                (["propiedades"], ["reportes", "analytics"],
                 "Las propiedades deben alimentar reportes de disponibilidad y rendimiento de cartera", "Visibilidad de cartera"),
            ],
            GapRules: [
                (["visitas", "cita", "agenda"], "Agenda de Visitas",
                 "Sin agenda de visitas la coordinación de showings es manual — pérdida de leads directa", "high"),
                (["pagos", "pago", "cobro"], "Control de Pagos",
                 "Sin módulo de pagos no hay trazabilidad financiera de arriendos o cuotas", "high"),
                (["documentos", "documento", "contrato"], "Gestión de Documentos",
                 "Sin documentos los contratos y escrituras no están centralizados — riesgo operacional", "medium"),
                (["reportes", "analytics"], "Reportes de Cartera",
                 "Sin reportes no hay visibilidad del rendimiento de la cartera de propiedades", "medium"),
            ],
            StarterNarrative: "El sistema cubre propiedades y clientes. Sin agenda de visitas ni control de pagos, el ciclo de venta y arriendo está incompleto.",
            GrowthNarrative:  "Buena cobertura comercial. El siguiente paso es integrar el control financiero de pagos y reportes de cartera.",
            MatureNarrative:  "Sistema de bienes raíces maduro. Considera agregar portal de clientes y analytics predictivo de disponibilidad.",
            StarterMilestone: "Agregar Agenda de Visitas y Control de Pagos para completar el ciclo comercial",
            GrowthMilestone:  "Implementar reportes de cartera y automatizar seguimiento de pagos"
        ),

        ["healthcare"] = new(
            IndustryLabel: "Salud",
            CoreModules:   ["pacientes", "citas", "medicos", "dashboard"],
            GrowthModules: ["historial", "medicamentos", "examenes", "facturacion"],
            AdvancedModules: ["laboratorio", "farmacia", "analytics", "telemedicina"],
            ConnectionRules: [
                (["citas", "agenda"], ["historial", "records", "clinico"],
                 "Cada consulta debe actualizar el historial clínico del paciente automáticamente", "Continuidad asistencial"),
                (["medicamentos", "prescripciones"], ["historial"],
                 "Las prescripciones deben quedar vinculadas al historial para trazabilidad", "Seguridad del paciente"),
                (["examenes", "laboratorio", "resultado"], ["historial", "records"],
                 "Los resultados de exámenes deben integrarse al historial del paciente", "Diagnóstico completo"),
                (["citas"], ["facturacion", "billing"],
                 "Las consultas completadas deben generar factura o liquidación de seguro", "Flujo de cobro"),
            ],
            GapRules: [
                (["historial", "clinico", "records"], "Historial Clínico",
                 "Sin historial clínico no hay continuidad asistencial — riesgo para el paciente", "high"),
                (["facturacion", "billing", "seguro"], "Facturación / Seguros",
                 "Sin facturación no hay control de ingresos ni liquidación con aseguradoras", "high"),
                (["medicamentos", "prescripciones", "farmacia"], "Prescripciones / Farmacia",
                 "Sin prescripciones los tratamientos no tienen trazabilidad clínica", "medium"),
                (["examenes", "laboratorio"], "Exámenes / Laboratorio",
                 "Sin módulo de exámenes los resultados no están integrados al flujo clínico", "medium"),
            ],
            StarterNarrative: "El sistema cubre pacientes y citas. Sin historial clínico y facturación, la práctica médica y el flujo financiero están incompletos.",
            GrowthNarrative:  "Buena cobertura clínica. El siguiente paso es cerrar el ciclo de cobro con facturación y habilitar prescripciones trazables.",
            MatureNarrative:  "Sistema de salud maduro. Considera telemedicina, integración con aseguradoras y analytics de salud poblacional.",
            StarterMilestone: "Agregar Historial Clínico y Facturación para completar el flujo básico",
            GrowthMilestone:  "Integrar laboratorio y prescripciones con el historial del paciente"
        ),

        ["education"] = new(
            IndustryLabel: "Educación",
            CoreModules:   ["estudiantes", "cursos", "instructores", "dashboard"],
            GrowthModules: ["matricula", "calificaciones", "asistencia", "pagos"],
            AdvancedModules: ["reportes", "certificados", "analytics", "portal"],
            ConnectionRules: [
                (["estudiantes", "alumno"], ["matricula", "inscripcion", "enrollment"],
                 "Los estudiantes deben tener historial completo de matrículas y cursos", "Trazabilidad académica"),
                (["cursos", "clase"], ["calificaciones", "notas", "grades"],
                 "Cada curso debe generar y registrar calificaciones por estudiante", "Evaluación académica"),
                (["matricula", "inscripcion"], ["pagos", "pago", "cobro"],
                 "La matrícula debe vincularse al flujo de pagos y aranceles", "Control financiero"),
                (["cursos", "clase"], ["asistencia", "attendance"],
                 "Los cursos deben tener control de asistencia integrado por sesión", "Control académico"),
            ],
            GapRules: [
                (["matricula", "inscripcion", "enrollment"], "Matrícula / Inscripción",
                 "Sin matrícula no hay registro formal de estudiantes por curso — brecha operacional", "high"),
                (["calificaciones", "notas", "grades"], "Calificaciones",
                 "Sin calificaciones el sistema no puede generar transcripts ni certificados", "high"),
                (["pagos", "pago", "cobro", "arancel"], "Gestión de Pagos",
                 "Sin control de pagos no hay trazabilidad de aranceles ni morosidad", "medium"),
                (["asistencia", "attendance"], "Control de Asistencia",
                 "Sin asistencia no hay datos para reportes de presencia ni cumplimiento", "medium"),
            ],
            StarterNarrative: "El sistema cubre estudiantes y cursos. Sin matrícula y calificaciones, el ciclo académico y administrativo está incompleto.",
            GrowthNarrative:  "Buena cobertura académica. El siguiente paso es integrar el control de pagos y habilitar generación de reportes y certificados.",
            MatureNarrative:  "Sistema educativo maduro. Considera portal de autoservicio para estudiantes y analytics de rendimiento académico.",
            StarterMilestone: "Agregar Matrícula y Calificaciones para completar el ciclo académico básico",
            GrowthMilestone:  "Integrar pagos y habilitar generación de certificados y reportes"
        ),

        ["ecommerce"] = new(
            IndustryLabel: "E-Commerce",
            CoreModules:   ["productos", "pedidos", "clientes", "dashboard"],
            GrowthModules: ["inventario", "pagos", "envios", "categorias"],
            AdvancedModules: ["reportes", "analytics", "crm", "marketing", "reviews"],
            ConnectionRules: [
                (["pedidos", "orden", "order"], ["inventario", "stock"],
                 "Los pedidos deben descontar stock automáticamente del inventario", "Control de stock"),
                (["pedidos", "orden"], ["pagos", "pago", "payment"],
                 "Los pedidos deben integrarse con el flujo de pagos y confirmación", "Flujo transaccional"),
                (["pedidos", "orden"], ["envios", "shipping", "logistica"],
                 "Los pedidos confirmados deben generar órdenes de envío automáticamente", "Logística"),
                (["productos"], ["reportes", "analytics"],
                 "Los productos deben alimentar reportes de ventas y análisis de inventario", "Visibilidad comercial"),
            ],
            GapRules: [
                (["pagos", "pago", "payment", "checkout"], "Pagos / Checkout",
                 "Sin pasarela de pagos el flujo de compra no puede completarse", "high"),
                (["inventario", "stock"], "Control de Inventario",
                 "Sin inventario los pedidos pueden aceptarse sin stock disponible — riesgo de sobre-venta", "high"),
                (["envios", "shipping", "logistica"], "Gestión de Envíos",
                 "Sin envíos los pedidos no tienen tracking ni gestión de entrega", "medium"),
                (["reportes", "analytics"], "Reportes de Ventas",
                 "Sin reportes no hay visibilidad de productos más vendidos ni tendencias", "medium"),
            ],
            StarterNarrative: "El sistema tiene productos y pedidos. Sin pagos e inventario, el ciclo de compra completo no funciona.",
            GrowthNarrative:  "Buena cobertura de e-commerce. El siguiente paso es integrar logística de envíos y analytics de ventas.",
            MatureNarrative:  "Plataforma e-commerce madura. Considera agregar CRM, marketing automatizado y sistema de reviews.",
            StarterMilestone: "Integrar Pagos e Inventario para completar el ciclo de compra",
            GrowthMilestone:  "Agregar Envíos y Reportes de Ventas para visibilidad operacional"
        ),

        ["logistics"] = new(
            IndustryLabel: "Logística",
            CoreModules:   ["vehiculos", "rutas", "conductores", "dashboard"],
            GrowthModules: ["pedidos", "clientes", "tracking", "mantenimiento"],
            AdvancedModules: ["reportes", "analytics", "optimizacion", "gps"],
            ConnectionRules: [
                (["vehiculos", "vehiculo", "vehicle"], ["conductores", "conductor", "driver"],
                 "Los vehículos deben estar asignados a conductores con historial de uso", "Control de flota"),
                (["rutas", "ruta", "route"], ["vehiculos", "vehicle"],
                 "Las rutas deben asignarse a vehículos disponibles según capacidad", "Optimización de rutas"),
                (["pedidos", "entrega", "delivery"], ["tracking", "rastreo"],
                 "Los pedidos en tránsito deben tener tracking en tiempo real", "Visibilidad de entregas"),
                (["vehiculos"], ["mantenimiento", "maintenance"],
                 "Los vehículos deben tener historial de mantenimiento preventivo", "Disponibilidad de flota"),
            ],
            GapRules: [
                (["tracking", "rastreo", "gps"], "Tracking en Tiempo Real",
                 "Sin tracking los clientes no tienen visibilidad de sus entregas — pérdida de confianza", "high"),
                (["reportes", "analytics"], "Reportes de Flota",
                 "Sin reportes no hay visibilidad de eficiencia, consumo o rendimiento por vehículo", "high"),
                (["mantenimiento", "maintenance"], "Mantenimiento de Flota",
                 "Sin mantenimiento preventivo las fallas imprevistas paralizan operaciones", "medium"),
            ],
            StarterNarrative: "El sistema cubre vehículos y rutas. Sin tracking y reportes de eficiencia, la operación no tiene visibilidad en tiempo real.",
            GrowthNarrative:  "Buen control de flota. El siguiente paso es agregar tracking en tiempo real y reportes de rendimiento por vehículo.",
            MatureNarrative:  "Sistema logístico maduro. Considera optimización de rutas con IA y analytics predictivo de mantenimiento.",
            StarterMilestone: "Implementar Tracking en tiempo real y asignación de conductores a vehículos",
            GrowthMilestone:  "Agregar Reportes de flota y optimización de rutas"
        ),

        ["general"] = new(
            IndustryLabel: "Plataforma General",
            CoreModules:   ["dashboard", "usuarios", "configuracion"],
            GrowthModules: ["reportes", "notificaciones", "auditoria"],
            AdvancedModules: ["analytics", "api", "integraciones", "billing"],
            ConnectionRules: [
                (["usuarios", "user"], ["auditoria", "audit", "log"],
                 "Las acciones de usuarios deben tener trazabilidad de auditoría completa", "Seguridad y compliance"),
                (["reportes"], ["dashboard"],
                 "Los reportes deben alimentar el dashboard con métricas consolidadas en tiempo real", "Visibilidad ejecutiva"),
            ],
            GapRules: [
                (["reportes", "analytics", "informe"], "Reportes / Analytics",
                 "Sin reportes los stakeholders no tienen visibilidad del estado del negocio", "high"),
                (["notificaciones", "alertas"], "Notificaciones",
                 "Sin notificaciones los usuarios no reciben alertas de eventos críticos del sistema", "medium"),
                (["auditoria", "audit", "log"], "Auditoría",
                 "Sin auditoría no hay trazabilidad de cambios — riesgo de compliance e integridad", "medium"),
            ],
            StarterNarrative: "El sistema tiene la base funcional. El siguiente paso es agregar reportes y notificaciones para dar visibilidad operacional.",
            GrowthNarrative:  "Buena cobertura funcional. Considera agregar auditoría para trazabilidad y analytics para decisiones basadas en datos.",
            MatureNarrative:  "Sistema maduro. El siguiente nivel es API pública, integraciones externas y capacidades de análisis avanzado.",
            StarterMilestone: "Agregar módulo de reportes y sistema de notificaciones",
            GrowthMilestone:  "Implementar auditoría completa y analytics de uso"
        ),
    };

    // ── Analysis ──────────────────────────────────────────────────────────────

    public async Task<IntelligenceReport> AnalyzeAsync(Guid productId, AppDbContext db, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Modules.Where(m => m.IsActive))
            .Include(p => p.Memory)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null)
            return Empty(productId);

        var industry = product.Memory
            .FirstOrDefault(m => m.Key == "industry")?.Value ?? "general";

        var profile = Profiles.TryGetValue(industry, out var p) ? p : Profiles["general"];

        // Searchable set built from all module name variations (lower-case)
        var moduleNames = product.Modules
            .SelectMany(m => new[] { m.ModuleName, m.EntityName, m.RoutePath })
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s.ToLowerInvariant())
            .ToHashSet();

        var moduleCount = product.Modules.Count;

        // ── Gap detection ─────────────────────────────────────────────────────
        var gaps = new List<IntelligenceGap>();
        foreach (var (keywords, module, reason, priority) in profile.GapRules)
        {
            if (!keywords.Any(k => moduleNames.Any(m => m.Contains(k))))
                gaps.Add(new IntelligenceGap(module, reason, priority, "missing_module"));
        }

        // ── Connection detection ──────────────────────────────────────────────
        var connections = new List<IntelligenceConnection>();
        foreach (var (aKws, bKws, label, impact) in profile.ConnectionRules)
        {
            var aExists = aKws.Any(k => moduleNames.Any(m => m.Contains(k)));
            var bExists = bKws.Any(k => moduleNames.Any(m => m.Contains(k)));

            // Surface only when at least one side of the connection exists
            if (aExists || bExists)
            {
                connections.Add(new IntelligenceConnection(
                    From:     Capitalize(aKws[0]),
                    To:       Capitalize(bKws[0]),
                    Label:    label,
                    Detected: aExists && bExists,
                    Impact:   impact
                ));
            }
        }

        // ── Suggestions: high-priority gaps + undetected connections ──────────
        var suggestions = new List<IntelligenceSuggestion>();

        foreach (var gap in gaps.Where(g => g.Priority == "high").Take(3))
        {
            suggestions.Add(new IntelligenceSuggestion(
                Title:    $"Agregar {gap.Module}",
                Context:  gap.Reason,
                Impact:   "Operaciones",
                Category: "operational"
            ));
        }

        foreach (var conn in connections.Where(c => !c.Detected).Take(2))
        {
            suggestions.Add(new IntelligenceSuggestion(
                Title:    $"Conectar {conn.From} → {conn.To}",
                Context:  conn.Label,
                Impact:   conn.Impact,
                Category: "automation"
            ));
        }

        // ── Evolution stage ───────────────────────────────────────────────────
        var coreHits   = profile.CoreModules.Count(k => moduleNames.Any(m => m.Contains(k)));
        var growthHits = profile.GrowthModules.Count(k => moduleNames.Any(m => m.Contains(k)));

        var (stage, stageLabel, milestone) = (coreHits, growthHits) switch
        {
            var (c, _) when c <= 1              => ("starter", "SaaS Inicial",         profile.StarterMilestone),
            var (c, g) when c >= 3 && g >= 2    => ("mature",  "SaaS Maduro",           profile.GrowthMilestone),
            _                                   => ("growth",  "SaaS en Crecimiento",   profile.StarterMilestone),
        };

        var narrative = stage switch
        {
            "starter" => profile.StarterNarrative,
            "mature"  => profile.MatureNarrative,
            _         => profile.GrowthNarrative,
        };

        // ── Health Score (0-100) ──────────────────────────────────────────────
        var detectedConnCount = connections.Count(c => c.Detected);
        var highGapCount      = gaps.Count(g => g.Priority == "high");

        var rawScore = Math.Min(coreHits * 15, 60)
                     + Math.Min(growthHits * 5,  30)
                     + Math.Min(detectedConnCount * 5, 15)
                     - highGapCount * 15;

        var healthNumeric = Math.Clamp(rawScore, 0, 100);

        var (healthScore, healthLabel) = healthNumeric switch
        {
            >= 75 => ("mature",      "Maduro"),
            >= 50 => ("growing",     "Creciendo"),
            >= 25 => ("operational", "Operacional"),
            _     => ("starter",     "Inicial"),
        };

        // ── Proactive Insights (top 4, highest severity first) ────────────────
        var topInsights = new List<ProactiveInsight>();

        foreach (var gap in gaps.Where(g => g.Priority == "high").Take(2))
            topInsights.Add(new ProactiveInsight(
                Type:     "critical_gap",
                Severity: "high",
                Title:    $"Falta {gap.Module}",
                Detail:   gap.Reason,
                Action:   $"Agregar {gap.Module}"
            ));

        foreach (var conn in connections.Where(c => !c.Detected && c.Impact != "").Take(2))
            topInsights.Add(new ProactiveInsight(
                Type:     "missing_connection",
                Severity: "medium",
                Title:    $"{conn.From} desconectado de {conn.To}",
                Detail:   conn.Label,
                Action:   $"Conectar {conn.From} → {conn.To}"
            ));

        foreach (var gap in gaps.Where(g => g.Priority == "medium").Take(1))
        {
            if (topInsights.Count < 3)
                topInsights.Add(new ProactiveInsight(
                    Type:     "gap_warning",
                    Severity: "medium",
                    Title:    $"Se recomienda {gap.Module}",
                    Detail:   gap.Reason,
                    Action:   $"Agregar {gap.Module}"
                ));
        }

        if (topInsights.Count < 2 && !string.IsNullOrEmpty(milestone))
            topInsights.Add(new ProactiveInsight(
                Type:     "evolution",
                Severity: "low",
                Title:    "Próximo hito operacional",
                Detail:   milestone,
                Action:   milestone
            ));

        return new IntelligenceReport(
            ProductId:             productId.ToString(),
            Industry:              industry,
            IndustryLabel:         profile.IndustryLabel,
            ModuleCount:           moduleCount,
            EvolutionStage:        stage,
            EvolutionStageLabel:   stageLabel,
            EvolutionNextMilestone:milestone,
            Gaps:                  gaps,
            Connections:           connections,
            Suggestions:           suggestions,
            Narrative:             narrative,
            AnalyzedAt:            DateTime.UtcNow,
            HealthScore:           healthScore,
            HealthScoreLabel:      healthLabel,
            HealthScoreNumeric:    healthNumeric,
            CriticalCount:         highGapCount,
            TopInsights:           topInsights
        );
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private static IntelligenceReport Empty(Guid productId) =>
        new(productId.ToString(), "general", "General", 0, "starter", "SaaS Inicial",
            "Agregar módulos base al sistema", [], [], [],
            "El sistema está en fase inicial.", DateTime.UtcNow,
            "starter", "Inicial", 0, 0, []);
}
