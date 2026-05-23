using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuilderPlatform.Infrastructure.Services;

// ── Domain records ──────────────────────────────────────────────────────────

public record SaturationPoint(
    string Title,
    string Description,
    string Severity,         // "critical" | "high" | "medium"
    string CollapseScenario,
    string AutomationFix,
    int    ScalingRisk       // 0-100
);

public record ManualOperation(
    string Title,
    string Description,
    string HumanCost,        // "critical" | "high" | "medium"
    string ImpactOnGrowth,
    string AutomationPath
);

public record AutomationOpportunity(
    string Title,
    string OperationalValue,
    string Impact,           // "transformational" | "high" | "medium"
    string Urgency,          // "immediate" | "near-term" | "later"
    string Unlocks
);

public record CapacityReport(
    string                      ProductId,
    string                      Industry,
    string                      IndustryLabel,
    int                         CapacityScore,
    string                      CapacityTier,
    string                      CapacityTierLabel,
    string                      ScalingNarrative,
    string                      TopRiskTitle,
    string                      TopRiskDescription,
    List<SaturationPoint>       SaturationPoints,
    List<ManualOperation>       ManualOperations,
    List<AutomationOpportunity> TopAutomationOpportunities,
    DateTime                    AnalyzedAt
);

// ── Profile sealed types ─────────────────────────────────────────────────────

sealed record AutomationWorkflow(string[] TriggerKeywords, string[] AutomationKeywords);
sealed record SaturationRule(string[] TriggerKeywords, string[] MissingKeywords, SaturationPoint Result);
sealed record ManualOpRule(string[] TriggerKeywords, string[] MissingKeywords, ManualOperation Result);
sealed record AutomationRule(string[] TriggerKeywords, string[] MissingKeywords, AutomationOpportunity Result);

sealed record CapacityProfile(
    string               IndustryKey,
    string               IndustryLabel,
    AutomationWorkflow[] Workflows,
    SaturationRule[]     SaturationRules,
    ManualOpRule[]       ManualOpRules,
    AutomationRule[]     AutomationRules
);

// ── Engine ───────────────────────────────────────────────────────────────────

public class ProductCapacityEngine
{
    private static readonly CapacityProfile[] Profiles = BuildProfiles();

    public async Task<CapacityReport> AnalyzeAsync(Guid productId, AppDbContext db, CancellationToken ct)
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

        // Automation coverage — how many critical workflows are connected
        int automatedWorkflows = profile.Workflows.Count(w => Has(w.TriggerKeywords) && Has(w.AutomationKeywords));
        int totalWorkflows      = profile.Workflows.Length;
        int coverage            = totalWorkflows == 0 ? 50 : automatedWorkflows * 100 / totalWorkflows;

        // Saturation points — fires when trigger exists but automation partner is missing
        var satPoints = profile.SaturationRules
            .Where(r => Has(r.TriggerKeywords) && !Has(r.MissingKeywords))
            .Select(r => r.Result)
            .OrderByDescending(s => s.ScalingRisk)
            .Take(6)
            .ToList();

        // Manual operations — fires when module exists but lacks automation
        var manuals = profile.ManualOpRules
            .Where(r => Has(r.TriggerKeywords) && !Has(r.MissingKeywords))
            .Select(r => r.Result)
            .Take(5)
            .ToList();

        // Automation opportunities — what to build next for maximum capacity
        var automations = profile.AutomationRules
            .Where(r => Has(r.TriggerKeywords) && !Has(r.MissingKeywords))
            .Select(r => r.Result)
            .Take(4)
            .ToList();

        // If no modules exist, surface all top automation opportunities
        if (product.Modules.Count == 0)
            automations = profile.AutomationRules.Take(3).Select(r => r.Result).ToList();

        // Score
        int critSat    = satPoints.Count(s => s.Severity == "critical");
        int highSat    = satPoints.Count(s => s.Severity == "high");
        int medSat     = satPoints.Count(s => s.Severity == "medium");
        int critManual = manuals.Count(m => m.HumanCost == "critical");
        int highManual = manuals.Count(m => m.HumanCost == "high");
        int satPenalty    = Math.Min(55, critSat * 20 + highSat * 10 + medSat * 5);
        int manualPenalty = Math.Min(20, critManual * 8 + highManual * 5);
        int score         = Math.Max(0, Math.Min(100, coverage - satPenalty - manualPenalty));

        string tier = score >= 75 ? "mature"
                    : score >= 50 ? "scalable"
                    : score >= 20 ? "partial"
                    : "manual";

        string tierLabel = tier switch
        {
            "mature"   => "Operación Escalable",
            "scalable" => "Parcialmente Escalable",
            "partial"  => "Operación con Riesgo de Escala",
            _          => "Operación Manual"
        };

        string narrative = BuildNarrative(tier, profile.IndustryLabel, satPoints, manuals, product.Modules.Count);
        var topRisk = satPoints.FirstOrDefault();

        return new CapacityReport(
            productId.ToString(), profile.IndustryKey, profile.IndustryLabel,
            score, tier, tierLabel, narrative,
            topRisk?.Title ?? "Sin puntos de saturación detectados",
            topRisk?.CollapseScenario ?? "La operación actual puede crecer sin riesgos inmediatos de saturación.",
            satPoints, manuals, automations, DateTime.UtcNow
        );
    }

    private static string BuildNarrative(string tier, string label, List<SaturationPoint> sat, List<ManualOperation> manuals, int moduleCount)
    {
        if (moduleCount == 0)
            return $"El sistema {label} está en fase inicial. Sin módulos, toda la operación depende de procesos manuales externos. Cualquier crecimiento requerirá contratar personas para cada proceso.";

        return tier switch
        {
            "mature"   => $"La operación {label} tiene buena cobertura de automatización. Los flujos críticos están conectados y el sistema puede escalar bajo crecimiento moderado sin saturación severa.",
            "scalable" => $"El sistema {label} escala en la mayoría de flujos. Quedan {sat.Count} punto(s) de saturación que pueden convertirse en cuellos operacionales en crecimiento acelerado.",
            "partial"  => $"La operación {label} funciona pero depende demasiado de coordinación manual. Con {sat.Count} punto(s) de saturación y {manuals.Count} operación(es) manuales críticas, el crecimiento generará fricción operacional severa.",
            _          => $"El sistema {label} opera casi totalmente de forma manual. Cualquier incremento significativo en volumen colapsará los flujos actuales. Automatizar los procesos core es urgente antes de escalar."
        };
    }

    private static CapacityReport Empty(string productId) =>
        new(productId, "general", "General", 0, "manual", "Operación Manual",
            "El sistema está en fase inicial. Sin módulos, toda la operación depende de procesos manuales externos.",
            "Sin módulos registrados", "Agrega módulos para detectar riesgos de capacidad operacional.",
            [], [], [], DateTime.UtcNow);

    // ── Industry profiles ─────────────────────────────────────────────────────

    private static CapacityProfile[] BuildProfiles() =>
    [
        // ── Restaurant ────────────────────────────────────────────────────────
        new("restaurant", "Restaurante",
            Workflows:
            [
                new(["pedido","orden","comanda"],    ["cocina","kds","kitchen"]),
                new(["inventario","stock"],           ["compra","proveedor","purchase"]),
                new(["personal","staff"],             ["planilla","nómina","payroll"]),
                new(["cliente","mesa"],               ["reserva","crm","fidelización"]),
                new(["caja","venta"],                 ["reporte","analytics","dashboard"]),
            ],
            SaturationRules:
            [
                new(["pedido","orden","comanda"], ["cocina","kds","kitchen"],
                    new("Pedidos sin KDS — Saturación en Hora Pico",
                        "Los pedidos llegan sin sistema digital a cocina. El mesero coordina verbalmente en cada turno.",
                        "critical",
                        "Con 3x pedidos simultáneos, la coordinación verbal colapsa: órdenes perdidas, tiempos de espera de 45+ min, rotación de clientes afectada.",
                        "Implementar KDS que envíe pedidos digitalmente a cocina, eliminando la coordinación verbal.",
                        92)),
                new(["inventario","stock"], ["compra","proveedor","purchase","alertas"],
                    new("Inventario sin Reposición Automática",
                        "El control de inventario existe pero la reposición depende de revisión manual del stock.",
                        "high",
                        "Con mayor volumen, los quiebres de stock sorpresivos aumentan. Un día sin ingredientes clave puede paralizar el servicio en hora pico.",
                        "Integrar módulo de Compras con alertas automáticas cuando el stock baja del mínimo.",
                        78)),
                new(["personal","staff"], ["planilla","nómina","payroll"],
                    new("Personal sin Planilla Automatizada",
                        "El equipo crece pero el cálculo de salarios y horas sigue siendo manual.",
                        "high",
                        "Con 15+ empleados, el cálculo manual de quincena consume 1-2 días completos y genera errores frecuentes.",
                        "Integrar Asistencia con Planilla para cálculo automático de horas, extras y deducciones.",
                        72)),
                new(["caja","venta"], ["reporte","analytics","dashboard"],
                    new("Ventas sin Reportes Automáticos",
                        "Las ventas se registran pero el análisis requiere exportar datos manualmente.",
                        "medium",
                        "El dueño no tiene visibilidad en tiempo real. Decisiones de menú, precios y horarios se toman con datos de días atrás.",
                        "Agregar Analytics con dashboard automático de ventas, platos más vendidos y horas pico.",
                        58)),
                new(["cliente","mesa"], ["reserva","crm","fidelización"],
                    new("Clientes sin Sistema de Fidelización",
                        "Los clientes son atendidos pero no hay seguimiento ni retención sistematizada.",
                        "medium",
                        "La adquisición de clientes es 100% orgánica. Sin fidelización, el crecimiento requiere marketing constante.",
                        "Implementar CRM básico con historial de visitas y programa de retención.",
                        48)),
            ],
            ManualOpRules:
            [
                new(["pedido","orden"], ["comanda","digital","app"],
                    new("Toma de Pedidos Manual",
                        "Los pedidos se toman en papel o de memoria, sin sistema digital.",
                        "high",
                        "Con más mesas, los errores en pedidos aumentan proporcionalmente. Cada error tiene costo en ingredientes y satisfacción.",
                        "Implementar comandas digitales o app de pedidos en mesa.")),
                new(["inventario"], ["alerta","automático","automática"],
                    new("Conteo de Inventario Manual",
                        "El inventario se revisa manualmente, generalmente al cierre o por intuición.",
                        "high",
                        "Sin alertas automáticas, los quiebres de stock se descubren cuando ya es tarde, afectando el servicio.",
                        "Configurar alertas automáticas de stock mínimo por producto.")),
                new(["caja"], ["cierre","automático"],
                    new("Cuadre de Caja Manual",
                        "El cierre de caja se hace contando billetes y comparando con registros manuales.",
                        "medium",
                        "Con mayor volumen de transacciones, el cuadre manual toma más tiempo y tiene más margen de error.",
                        "Integrar sistema POS con cierre automático y conciliación.")),
            ],
            AutomationRules:
            [
                new(["pedido","orden"], ["cocina","kds"],
                    new("KDS — Automatizar Flujo Pedidos → Cocina",
                        "Un KDS elimina la coordinación verbal y puede reducir tiempos de entrega en 30-40% durante hora pico.",
                        "transformational", "immediate",
                        "Reducción de errores, tiempos de espera y saturación en hora pico.")),
                new(["inventario"], ["compra","proveedor"],
                    new("Auto-Compras — Reposición Automática de Stock",
                        "Cuando el stock baja del mínimo, se genera automáticamente una orden de compra al proveedor.",
                        "high", "near-term",
                        "Eliminación de quiebres de stock y reducción de tiempo de gestión de inventario.")),
                new(["caja","venta"], ["reporte","analytics"],
                    new("Analytics — Reportes Automáticos de Ventas",
                        "Dashboard en tiempo real con ventas por hora, platos más vendidos y comparativas semanales.",
                        "high", "near-term",
                        "Decisiones basadas en datos e identificación de horas pico para optimización de operación.")),
                new(["personal"], ["planilla","nómina"],
                    new("Planilla desde Asistencia — Eliminar Cálculo Manual",
                        "Conectar asistencia con planilla para que el cálculo de nómina sea automático al cerrar quincena.",
                        "high", "near-term",
                        "Eliminación de 1-2 días de trabajo manual por quincena.")),
            ]
        ),

        // ── HR / Payroll ───────────────────────────────────────────────────────
        new("hr_payroll", "RRHH y Planilla",
            Workflows:
            [
                new(["asistencia","attendance"],  ["planilla","nómina","payroll"]),
                new(["empleado","employee"],       ["onboarding","incorporación"]),
                new(["vacaciones","permiso"],      ["aprobación","approval","digital"]),
                new(["contrato"],                  ["firma","digital","electrónica"]),
                new(["planilla","nómina"],         ["reporte","compliance","auditoría"]),
            ],
            SaturationRules:
            [
                new(["asistencia","attendance"], ["planilla","nómina","payroll"],
                    new("Asistencia sin Cálculo Automático de Planilla",
                        "La asistencia se registra pero el cálculo de nómina se hace manualmente en Excel o similar.",
                        "critical",
                        "Con 30+ empleados, el proceso manual consume 3-4 días por quincena con alta probabilidad de errores y disputas salariales.",
                        "Conectar Asistencia con Planilla para cálculo automático de horas, extras y deducciones.",
                        95)),
                new(["vacaciones","permiso","ausencia"], ["aprobación","approval","flujo"],
                    new("Vacaciones sin Flujo de Aprobación Digital",
                        "Las solicitudes de vacaciones van por WhatsApp, email o papel.",
                        "high",
                        "Con equipo grande, el seguimiento de vacaciones aprobadas vs rechazadas se vuelve caótico, generando conflictos y errores en planilla.",
                        "Implementar solicitudes digitales con flujo de aprobación y sincronización automática con planilla.",
                        80)),
                new(["empleado","employee"], ["auditoría","audit","trazabilidad"],
                    new("Empleados sin Auditoría de Cambios",
                        "Los cambios en datos de empleados no quedan registrados con quién y cuándo.",
                        "high",
                        "Sin trazabilidad, es imposible responder ante disputas laborales o inspecciones. El riesgo legal escala con el equipo.",
                        "Activar auditoría completa de cambios con timestamp y usuario responsable.",
                        75)),
                new(["planilla","nómina"], ["reporte","compliance"],
                    new("Planilla sin Reportes de Compliance",
                        "La planilla se calcula pero no genera automáticamente los reportes regulatorios requeridos.",
                        "medium",
                        "Con crecimiento, el cumplimiento regulatorio manual consume más tiempo y genera riesgo de multas.",
                        "Agregar módulo de reportes automáticos para cumplimiento tributario y cargas sociales.",
                        62)),
                new(["empleado"], ["onboarding","incorporación"],
                    new("Empleados sin Onboarding Estructurado",
                        "Cada empleado nuevo se incorpora ad-hoc sin checklist ni seguimiento digital.",
                        "medium",
                        "Con rotación o crecimiento acelerado, la incorporación deficiente genera baja productividad inicial.",
                        "Implementar flujo de onboarding digital con checklist, documentos y seguimiento.",
                        50)),
            ],
            ManualOpRules:
            [
                new(["asistencia"], ["integración","integrado","automático"],
                    new("Asistencia sin Integración a Planilla",
                        "Los registros de asistencia se exportan manualmente para incluir en el cálculo de nómina.",
                        "critical",
                        "Cada quincena requiere conciliación manual de datos entre sistemas, generando errores y tiempo desperdiciado.",
                        "Integrar directamente asistencia con planilla para eliminación de reproceso.")),
                new(["planilla","nómina"], ["boleta","comprobante","reporte"],
                    new("Boletas de Pago Manuales",
                        "Las boletas de pago se generan manualmente en Excel y se envían por WhatsApp.",
                        "high",
                        "Con 20+ empleados, la generación manual toma horas y tiene riesgo de errores en montos.",
                        "Automatizar generación y envío de boletas desde el sistema de planilla.")),
                new(["empleado"], ["contrato","firma"],
                    new("Contratos en Papel",
                        "Los contratos laborales se firman en papel y se guardan físicamente.",
                        "medium",
                        "La búsqueda y gestión de contratos físicos es ineficiente y riesgosa ante inspecciones.",
                        "Implementar firma electrónica y gestión digital de contratos.")),
            ],
            AutomationRules:
            [
                new(["asistencia"], ["planilla","nómina"],
                    new("Planilla Automática desde Asistencia",
                        "El mayor impacto en RRHH: calcular nómina automáticamente desde asistencia elimina 3-4 días de trabajo manual por quincena.",
                        "transformational", "immediate",
                        "Eliminación de reproceso, errores de cálculo y disputas salariales.")),
                new(["vacaciones"], ["aprobación","digital"],
                    new("Aprobaciones Digitales de Vacaciones",
                        "Flujo digital solicitud → aprobación → registro automático en planilla, sin email ni papel.",
                        "high", "near-term",
                        "Control en tiempo real de ausencias y mejor planificación del equipo.")),
                new(["planilla"], ["reporte","compliance"],
                    new("Reportes de Compliance Automáticos",
                        "Generación automática de reportes para CCSS, Hacienda y otras entidades al cerrar planilla.",
                        "high", "near-term",
                        "Reducción de riesgo legal y eliminación de trabajo manual de cumplimiento regulatorio.")),
                new(["empleado"], ["auditoría"],
                    new("Auditoría Automática de Cambios",
                        "Registrar quién cambia qué en datos de empleados, con timestamp y motivo.",
                        "high", "near-term",
                        "Trazabilidad completa para inspecciones laborales y resolución de disputas.")),
            ]
        ),

        // ── Veterinary ────────────────────────────────────────────────────────
        new("veterinary", "Veterinaria",
            Workflows:
            [
                new(["cita","turno","appointment"],    ["recordatorio","reminder","notificación"]),
                new(["paciente","mascota","animal"],   ["historial","expediente","historia"]),
                new(["medicamento","medicina"],        ["alerta","stock","inventario"]),
                new(["consulta","visita"],             ["factura","cobro","pago"]),
                new(["seguimiento","control"],         ["comunicación","automatizado","notificación"]),
            ],
            SaturationRules:
            [
                new(["paciente","mascota"], ["historial","expediente","historia clínica"],
                    new("Pacientes sin Historial Digital Integrado",
                        "El historial del paciente existe separado o en papel, no integrado con las citas.",
                        "critical",
                        "Con 200+ pacientes activos, el médico reconstruye la historia en cada consulta. El riesgo de diagnósticos incompletos y alergias omitidas crece significativamente.",
                        "Integrar Pacientes con historial clínico accesible desde la consulta.",
                        90)),
                new(["cita","turno","appointment"], ["recordatorio","reminder","notificación"],
                    new("Citas sin Recordatorios Automáticos",
                        "Los recordatorios los hace la recepcionista llamando uno a uno.",
                        "high",
                        "Con 50+ citas semanales, la recepcionista dedica 2-3 horas diarias solo a llamadas de confirmación, saturando el teléfono.",
                        "Implementar recordatorios automáticos por WhatsApp o SMS antes de cada cita.",
                        80)),
                new(["medicamento","medicina"], ["alerta","automático","stock mínimo"],
                    new("Medicamentos sin Alertas de Stock",
                        "El inventario de medicamentos se revisa manualmente, generalmente cuando ya hay desabasto.",
                        "high",
                        "Un medicamento faltante durante una emergencia puede comprometer la atención. Con mayor volumen, los quiebres son más frecuentes.",
                        "Configurar alertas automáticas de stock mínimo por medicamento crítico.",
                        75)),
                new(["consulta","visita"], ["seguimiento","recordatorio","control"],
                    new("Consultas sin Seguimiento Automatizado",
                        "Después de cada consulta, el seguimiento depende de que el dueño recuerde llamar.",
                        "medium",
                        "Sin seguimiento sistemático, los controles post-operatorios y vacunaciones se pierden.",
                        "Automatizar recordatorios de seguimiento y controles programados.",
                        60)),
            ],
            ManualOpRules:
            [
                new(["cita","turno"], ["agenda","digital","online"],
                    new("Agenda de Citas Manual",
                        "Las citas se agendan por teléfono y se anotan en agenda física o Excel.",
                        "high",
                        "Con crecimiento, el cruce de citas, confirmaciones y cancelaciones consume tiempo de recepción.",
                        "Implementar agenda digital con autogestión de citas para clientes.")),
                new(["paciente"], ["recordatorio","vacuna","próxima"],
                    new("Gestión de Vacunas y Controles Manual",
                        "Los calendarios de vacunación y controles se gestionan manualmente.",
                        "medium",
                        "Sin sistema automatizado, muchos dueños no regresan para vacunaciones de rutina, perdiendo ingresos recurrentes.",
                        "Automatizar el calendario de vacunas con recordatorios proactivos.")),
                new(["consulta"], ["factura","facturación","electrónica"],
                    new("Facturación Manual por Consulta",
                        "Cada factura se genera manualmente después de la consulta.",
                        "medium",
                        "Con mayor volumen, la facturación se convierte en cuello al cierre del día.",
                        "Integrar facturación electrónica desde la consulta.")),
            ],
            AutomationRules:
            [
                new(["paciente","mascota"], ["historial","expediente"],
                    new("Historial Digital — Eliminar Reconstrucción Manual",
                        "Con historial integrado, el médico accede en segundos a alergias, medicamentos y diagnósticos previos.",
                        "transformational", "immediate",
                        "Seguridad clínica, consultas más eficientes y mejor continuidad de atención.")),
                new(["cita","turno"], ["recordatorio","notificación"],
                    new("Recordatorios Automáticos — Liberar Recepción",
                        "WhatsApp/SMS automáticos liberan 2-3 horas diarias de la recepcionista para tareas de mayor valor.",
                        "high", "near-term",
                        "Reducción de inasistencias y recepción más eficiente.")),
                new(["medicamento"], ["alerta","stock"],
                    new("Alertas de Stock — Prevenir Quiebres",
                        "Notificación automática cuando medicamentos críticos bajan del nivel mínimo.",
                        "high", "near-term",
                        "Eliminación de situaciones de emergencia por falta de medicamentos.")),
                new(["consulta"], ["seguimiento"],
                    new("Seguimiento Post-Consulta Automatizado",
                        "Recordatorios programados de controles, vacunas y medicamentos después de cada consulta.",
                        "high", "later",
                        "Mayor recurrencia de clientes y mejor calidad de atención.")),
            ]
        ),

        // ── E-Commerce ────────────────────────────────────────────────────────
        new("ecommerce", "E-Commerce",
            Workflows:
            [
                new(["producto","catálogo"],        ["inventario","stock","disponibilidad"]),
                new(["orden","pedido","compra"],    ["pago","payment","cobro"]),
                new(["orden","pedido"],             ["envío","shipping","logística"]),
                new(["cliente","usuario"],          ["notificación","email","comunicación"]),
                new(["devolución","reembolso"],     ["proceso","automático","flujo"]),
            ],
            SaturationRules:
            [
                new(["orden","pedido","compra"], ["pago","payment"],
                    new("Órdenes sin Pago Automático",
                        "Las órdenes se reciben pero el cobro requiere confirmación manual.",
                        "critical",
                        "Con volumen, el proceso manual de confirmación crea órdenes en espera, clientes ansiosos y pérdida de ventas.",
                        "Integrar pasarela de pago para cobro automático e inmediato.",
                        92)),
                new(["producto","catálogo"], ["inventario","stock"],
                    new("Catálogo sin Sincronización de Inventario",
                        "Los productos se muestran sin reflejar el stock real en tiempo real.",
                        "high",
                        "Se venden productos sin existencia, generando cancelaciones, devoluciones y pérdida de confianza del cliente.",
                        "Sincronizar catálogo con inventario para actualizar disponibilidad automáticamente.",
                        85)),
                new(["orden","pedido"], ["envío","shipping","tracking"],
                    new("Pedidos sin Sistema de Envío Integrado",
                        "Cada pedido requiere proceso manual de generación de guía y comunicación con transportista.",
                        "high",
                        "Con más pedidos, el proceso de envío se convierte en cuello de botella y los clientes no tienen visibilidad.",
                        "Integrar con logística para generación automática de guías y tracking.",
                        78)),
                new(["cliente"], ["notificación","email","automático"],
                    new("Clientes sin Comunicación Automatizada",
                        "Las actualizaciones de estado de pedido se envían manualmente o no se envían.",
                        "medium",
                        "Los clientes generan soporte innecesario al no saber el estado de su pedido. El volumen de preguntas escala con las ventas.",
                        "Automatizar notificaciones de confirmación, envío y entrega.",
                        60)),
            ],
            ManualOpRules:
            [
                new(["orden","pedido"], ["procesamiento","automático","fulfillment"],
                    new("Procesamiento de Órdenes Manual",
                        "Cada orden requiere que alguien la revise, confirme y prepare manualmente.",
                        "high",
                        "El tiempo de procesamiento manual limita cuántos pedidos se pueden gestionar por día.",
                        "Implementar flujo automático de confirmación, picking y preparación.")),
                new(["inventario","stock"], ["reposición","compra","proveedor"],
                    new("Reposición de Inventario Manual",
                        "Los pedidos de reposición a proveedores se hacen manualmente cuando se nota el quiebre.",
                        "high",
                        "Con mayor rotación, los quiebres de stock frecuentes generan ventas perdidas.",
                        "Configurar alertas automáticas de stock mínimo y órdenes de compra automáticas.")),
            ],
            AutomationRules:
            [
                new(["orden","pedido"], ["pago","payment"],
                    new("Pasarela de Pago — Automatizar el Cobro",
                        "El cobro automático es el paso más crítico: sin él, cada venta requiere intervención manual.",
                        "transformational", "immediate",
                        "Ventas 24/7 sin intervención humana y reducción de abandono en checkout.")),
                new(["producto"], ["inventario"],
                    new("Sincronización de Inventario en Tiempo Real",
                        "Actualizar automáticamente la disponibilidad al venderse para evitar overselling.",
                        "high", "immediate",
                        "Eliminación de cancelaciones por falta de stock y mejor experiencia de compra.")),
                new(["cliente"], ["notificación"],
                    new("Comunicación Automática de Estado de Pedido",
                        "Notificaciones automáticas en cada etapa reducen soporte en 60-70%.",
                        "high", "near-term",
                        "Reducción de carga de soporte y mejor NPS.")),
            ]
        ),

        // ── Logistics ─────────────────────────────────────────────────────────
        new("logistics", "Logística",
            Workflows:
            [
                new(["ruta","entrega","route"],    ["optimización","planificación"]),
                new(["vehículo","flota"],           ["mantenimiento","alerta"]),
                new(["conductor","driver"],         ["tracking","ubicación","gps"]),
                new(["paquete","envío","carga"],    ["tracking","estado","trazabilidad"]),
                new(["cliente"],                    ["notificación","comunicación"]),
            ],
            SaturationRules:
            [
                new(["ruta","entrega"], ["optimización","algoritmo","planificación"],
                    new("Rutas sin Optimización Automática",
                        "Las rutas de entrega se asignan manualmente por el coordinador cada mañana.",
                        "high",
                        "Con más conductores y entregas, la planificación manual genera rutas ineficientes, mayor combustible y tiempos de entrega más largos.",
                        "Implementar optimizador de rutas para asignación automática basada en ubicación y capacidad.",
                        80)),
                new(["vehículo","flota"], ["mantenimiento","alerta","preventivo"],
                    new("Flota sin Mantenimiento Predictivo",
                        "El mantenimiento de vehículos se hace por calendario fijo o cuando el conductor reporta una falla.",
                        "high",
                        "Una falla no prevista en ruta genera costos de emergencia, retrasos y pérdida de clientes.",
                        "Implementar alertas automáticas de mantenimiento basadas en kilómetros y reportes.",
                        75)),
                new(["paquete","envío","carga"], ["tracking","estado","trazabilidad"],
                    new("Envíos sin Tracking en Tiempo Real",
                        "El estado de los envíos no es visible en tiempo real para clientes ni para la operación.",
                        "medium",
                        "Sin visibilidad, los clientes generan múltiples consultas de estado, saturando el equipo de soporte.",
                        "Implementar tracking con actualizaciones automáticas de estado.",
                        60)),
            ],
            ManualOpRules:
            [
                new(["ruta","conductor"], ["asignación","automática","digital"],
                    new("Asignación de Rutas Manual",
                        "El coordinador asigna rutas a conductores manualmente cada mañana.",
                        "high",
                        "Con crecimiento de flota, el tiempo de asignación manual escala y la eficiencia de rutas disminuye.",
                        "Automatizar la asignación de rutas según capacidad del vehículo y zona.")),
                new(["paquete"], ["confirmación","entrega","firma"],
                    new("Confirmación de Entrega Manual",
                        "La confirmación de entrega exitosa se hace por llamada o WhatsApp.",
                        "medium",
                        "Sin confirmación digital, el seguimiento de entregas completadas requiere conciliación manual.",
                        "Implementar firma digital o confirmación automática en app de conductor.")),
            ],
            AutomationRules:
            [
                new(["ruta"], ["optimización"],
                    new("Optimización Automática de Rutas",
                        "Reducción de hasta 25% en combustible y 30% en tiempo de entrega con rutas optimizadas.",
                        "transformational", "near-term",
                        "Mayor cantidad de entregas por vehículo y reducción de costos operativos.")),
                new(["conductor"], ["tracking","gps"],
                    new("Tracking GPS en Tiempo Real",
                        "Visibilidad completa de la flota permite reasignaciones dinámicas y comunicación proactiva con clientes.",
                        "high", "near-term",
                        "Reducción de consultas de estado y mejor gestión de emergencias.")),
            ]
        ),

        // ── Real Estate ───────────────────────────────────────────────────────
        new("real_estate", "Bienes Raíces",
            Workflows:
            [
                new(["propiedad","inmueble"],   ["portal","publicación","listado"]),
                new(["cliente","prospecto"],    ["crm","seguimiento"]),
                new(["visita","tour"],          ["agenda","confirmación"]),
                new(["contrato","documento"],   ["firma","digital"]),
                new(["alquiler","renta"],       ["cobro","pago","automático"]),
            ],
            SaturationRules:
            [
                new(["cliente","prospecto"], ["crm","seguimiento","pipeline"],
                    new("Prospectos sin CRM Estructurado",
                        "Los prospectos se gestionan por WhatsApp, email o cuadernos sin sistema centralizado.",
                        "high",
                        "Con más agentes, los prospectos se pierden entre comunicaciones, generando oportunidades de venta desaprovechadas.",
                        "Implementar CRM con pipeline visual, historial de contactos y recordatorios automáticos.",
                        78)),
                new(["propiedad","inmueble"], ["portal","publicación","mls"],
                    new("Propiedades sin Publicación Automatizada",
                        "Las propiedades se publican manualmente en cada portal por separado.",
                        "high",
                        "Con más propiedades, mantener múltiples portales actualizados manualmente consume horas y genera información inconsistente.",
                        "Integrar con portales inmobiliarios para publicación automática.",
                        72)),
                new(["alquiler","renta"], ["cobro","automático","pago"],
                    new("Alquileres sin Cobro Automatizado",
                        "El cobro de alquileres se gestiona manualmente con seguimiento de morosos por mensaje.",
                        "medium",
                        "Con más propiedades en alquiler, el seguimiento manual de pagos consume tiempo administrativo desproporcionado.",
                        "Automatizar el cobro mensual de alquileres con recordatorios y gestión de mora.",
                        55)),
            ],
            ManualOpRules:
            [
                new(["visita","tour"], ["agenda","confirmación","automático"],
                    new("Coordinación de Visitas Manual",
                        "Las visitas se coordinan por teléfono o WhatsApp sin sistema de agenda.",
                        "high",
                        "Los conflictos de horarios y visitas sin confirmar generan oportunidades perdidas.",
                        "Implementar agenda digital con autoconfirmación y recordatorios.")),
                new(["contrato"], ["firma","electrónica","digital"],
                    new("Contratos en Papel",
                        "Los contratos se firman físicamente, requiriendo presencia o correo.",
                        "medium",
                        "El proceso de firma retrasa cierres y genera logística innecesaria.",
                        "Implementar firma electrónica para contratos.")),
            ],
            AutomationRules:
            [
                new(["cliente","prospecto"], ["crm"],
                    new("CRM — Centralizar Gestión de Prospectos",
                        "Un CRM elimina la pérdida de prospectos y permite seguimiento sistemático del pipeline.",
                        "transformational", "immediate",
                        "Aumento de tasa de cierre por seguimiento consistente.")),
                new(["propiedad"], ["portal"],
                    new("Publicación Automática en Portales",
                        "Sincronizar propiedades con portales automáticamente elimina trabajo repetitivo.",
                        "high", "near-term",
                        "Mayor exposición de propiedades con menos tiempo de gestión.")),
            ]
        ),

        // ── Healthcare ────────────────────────────────────────────────────────
        new("healthcare", "Salud",
            Workflows:
            [
                new(["cita","consulta","appointment"], ["recordatorio","confirmación"]),
                new(["paciente"],                       ["historial","expediente"]),
                new(["diagnóstico","tratamiento"],      ["seguimiento","monitoreo"]),
                new(["laboratorio","examen"],           ["resultado","notificación"]),
                new(["factura","cobro"],                ["seguro","aseguradora","automático"]),
            ],
            SaturationRules:
            [
                new(["cita","consulta"], ["historial","expediente","historia clínica"],
                    new("Consultas sin Historial Integrado",
                        "El médico no tiene acceso inmediato al historial completo durante la consulta.",
                        "critical",
                        "Con mayor volumen, el tiempo de reconstrucción de historial en cada consulta se vuelve clínicamente peligroso e ineficiente.",
                        "Integrar Citas con historial clínico digital del paciente.",
                        90)),
                new(["laboratorio","examen"], ["resultado","notificación","entrega"],
                    new("Resultados de Laboratorio sin Entrega Automática",
                        "Los resultados se entregan manualmente al paciente en persona o por llamada.",
                        "high",
                        "Con más pacientes, el proceso de notificación satura la recepción y genera ansiedad.",
                        "Automatizar la entrega de resultados por portal con notificación automática.",
                        75)),
                new(["diagnóstico","tratamiento"], ["seguimiento","monitoreo"],
                    new("Tratamientos sin Seguimiento Automatizado",
                        "El seguimiento del tratamiento depende de que el paciente regrese por iniciativa propia.",
                        "medium",
                        "Sin seguimiento sistemático, los tratamientos incompletos generan readmisiones evitables.",
                        "Implementar seguimiento con recordatorios automáticos de citas de control.",
                        58)),
            ],
            ManualOpRules:
            [
                new(["cita","consulta"], ["agenda","digital","autogestión"],
                    new("Agenda Médica Manual",
                        "Las citas se agendan por teléfono sin posibilidad de autogestión del paciente.",
                        "high",
                        "El personal de recepción dedica la mayor parte del tiempo a agendar y confirmar citas telefónicamente.",
                        "Implementar portal de autoagendado para que pacientes gestionen sus propias citas.")),
                new(["factura","cobro"], ["seguro","aseguradora"],
                    new("Gestión de Seguros Manual",
                        "La facturación a aseguradoras se hace manualmente con formularios separados por aseguradora.",
                        "medium",
                        "Con más pacientes asegurados, la gestión manual de reclamaciones consume días.",
                        "Integrar con aseguradoras principales para facturación y reclamaciones automáticas.")),
            ],
            AutomationRules:
            [
                new(["paciente"], ["historial"],
                    new("Historial Clínico Digital Integrado",
                        "Con historial integrado, cada consulta tiene acceso inmediato a alergias, medicamentos y diagnósticos previos.",
                        "transformational", "immediate",
                        "Seguridad clínica y consultas más eficientes.")),
                new(["cita"], ["recordatorio"],
                    new("Recordatorios Automáticos de Citas",
                        "Reducción de inasistencias en 30-40% con recordatorios automáticos 48h y 24h antes.",
                        "high", "near-term",
                        "Mayor utilización de agenda y reducción de huecos por inasistencias.")),
                new(["laboratorio"], ["resultado"],
                    new("Entrega Digital de Resultados",
                        "Notificación automática cuando los resultados están listos, sin llamadas de seguimiento.",
                        "high", "near-term",
                        "Liberación de carga de recepción y mejor experiencia del paciente.")),
            ]
        ),

        // ── Education ─────────────────────────────────────────────────────────
        new("education", "Educación",
            Workflows:
            [
                new(["estudiante","alumno"],   ["matrícula","inscripción"]),
                new(["curso","clase"],         ["asistencia","registro"]),
                new(["calificación","nota"],   ["reporte","boletín"]),
                new(["pago","mensualidad"],    ["automático","recordatorio"]),
                new(["docente","profesor"],    ["comunicación","plataforma"]),
            ],
            SaturationRules:
            [
                new(["estudiante","alumno"], ["matrícula","inscripción","digital"],
                    new("Estudiantes sin Matrícula Digital",
                        "El proceso de inscripción se hace en persona o por formularios en papel.",
                        "high",
                        "Con crecimiento, las matrículas presenciales generan cuellos en temporada de inscripción, perdiendo prospectos que no quieren esperar.",
                        "Implementar matrícula digital con formulario en línea, pago y confirmación automática.",
                        75)),
                new(["calificación","nota"], ["reporte","boletín","automático"],
                    new("Calificaciones sin Reportes Automáticos",
                        "Las notas se registran pero los boletines se generan manualmente al finalizar el período.",
                        "medium",
                        "Con más estudiantes y docentes, la generación manual de boletines consume semanas con alta probabilidad de errores.",
                        "Automatizar la generación de boletines desde las calificaciones registradas.",
                        60)),
                new(["pago","mensualidad"], ["automático","recordatorio","cobro"],
                    new("Pagos sin Cobro Automatizado",
                        "Los pagos de mensualidades se gestionan manualmente con seguimiento de morosos por llamada.",
                        "medium",
                        "Con más estudiantes, el seguimiento manual de morosidad consume tiempo administrativo y genera tensión.",
                        "Automatizar recordatorios de pago y gestión de mora con notificaciones digitales.",
                        55)),
            ],
            ManualOpRules:
            [
                new(["curso","clase"], ["asistencia","registro","digital"],
                    new("Registro de Asistencia Manual",
                        "La asistencia se toma en lista papel o Excel por cada docente.",
                        "high",
                        "Con más grupos y docentes, la consolidación de asistencia manual tarda días y genera discrepancias.",
                        "Implementar sistema digital de asistencia con app o QR para docentes.")),
                new(["comunicación","notificación"], ["plataforma","app","portal"],
                    new("Comunicación Institucional por WhatsApp",
                        "Las comunicaciones con padres y estudiantes van por grupos de WhatsApp no estructurados.",
                        "medium",
                        "Sin canal institucional, la información se pierde y no hay registro de comunicaciones.",
                        "Implementar portal o app institucional para comunicaciones estructuradas.")),
            ],
            AutomationRules:
            [
                new(["estudiante"], ["matrícula"],
                    new("Matrícula Digital — Eliminar Filas de Inscripción",
                        "Proceso 100% digital: formulario, pago y confirmación automática en minutos.",
                        "transformational", "near-term",
                        "Mayor conversión de prospectos y eliminación de cuellos en temporada de inscripción.")),
                new(["pago","mensualidad"], ["automático"],
                    new("Cobro Automático de Mensualidades",
                        "Recordatorios automáticos y pagos en línea reducen morosidad y tiempo administrativo.",
                        "high", "near-term",
                        "Mejor flujo de caja y reducción de tiempo en gestión de cobros.")),
                new(["calificación"], ["reporte"],
                    new("Boletines Automáticos por Período",
                        "Generación automática de boletines al cerrar el período académico.",
                        "high", "later",
                        "Eliminación de semanas de trabajo manual con mayor precisión.")),
            ]
        ),

        // ── General ───────────────────────────────────────────────────────────
        new("general", "General",
            Workflows:
            [
                new(["usuario","cliente"],  ["notificación","comunicación"]),
                new(["proceso","flujo"],    ["automatización","automático"]),
                new(["reporte","datos"],    ["dashboard","analytics"]),
                new(["tarea","trabajo"],    ["asignación","seguimiento"]),
            ],
            SaturationRules:
            [
                new(["proceso","flujo","operación"], ["automatización","automático"],
                    new("Procesos Core sin Automatización",
                        "Los procesos principales dependen de intervención manual en cada paso.",
                        "high",
                        "Sin automatización, cualquier crecimiento en volumen requiere contratar más personas linealmente.",
                        "Identificar los 2-3 procesos más repetitivos y automatizarlos primero.",
                        75)),
                new(["usuario","cliente"], ["notificación","comunicación","automático"],
                    new("Usuarios sin Comunicación Automatizada",
                        "Las comunicaciones a usuarios se envían manualmente o no se envían.",
                        "medium",
                        "Sin automatización de comunicaciones, el soporte y la retención escalan mal.",
                        "Implementar notificaciones automáticas para eventos clave del sistema.",
                        55)),
                new(["datos","información"], ["reporte","dashboard","analytics"],
                    new("Datos sin Reportes Automáticos",
                        "La información del sistema se extrae manualmente para análisis.",
                        "medium",
                        "Sin reportes automáticos, la toma de decisiones depende de trabajo manual que escala mal.",
                        "Implementar dashboard con reportes automáticos de métricas clave.",
                        50)),
            ],
            ManualOpRules:
            [
                new(["proceso"], ["automatización"],
                    new("Flujos Operativos Manuales",
                        "Los flujos de trabajo principales requieren intervención humana en cada paso.",
                        "high",
                        "La operación no escala sin contratar personal adicional proporcionalmente.",
                        "Mapear y automatizar los flujos más repetitivos primero.")),
            ],
            AutomationRules:
            [
                new(["proceso"], ["automatización"],
                    new("Automatizar Flujos Operativos Core",
                        "Identificar los 2-3 procesos más repetitivos y construir automatización simple.",
                        "high", "near-term",
                        "Escalabilidad operacional sin crecimiento lineal de personal.")),
                new(["usuario","cliente"], ["notificación"],
                    new("Comunicación Automática con Usuarios",
                        "Notificaciones automáticas en eventos clave reducen carga de soporte.",
                        "high", "near-term",
                        "Reducción de soporte inbound y mejor experiencia de usuario.")),
            ]
        ),
    ];
}
