namespace BuilderPlatform.Infrastructure.Services;

public record ProductProfile(
    string Industry,
    string IndustryLabel,
    string SaasType,
    string Problem,
    string TargetUser,
    string[] CoreFeatures,
    string[] DbEntities,
    string ArchitecturePattern,
    string TechnicalRisk,
    string[] SprintPlan
);

public static class ContentGenerator
{
    private static readonly Dictionary<string, ProductProfile> IndustryProfiles = new()
    {
        ["veterinary"] = new(
            "veterinary", "Veterinaria / Salud Animal", "B2B Multi-tenant",
            "Clínicas veterinarias gestionan citas, historiales médicos y facturación en sistemas desconectados o en papel, perdiendo trazabilidad y eficiencia operacional.",
            "Veterinarios y administradores de clínicas con 1-15 empleados.",
            ["Pacientes (mascotas) y propietarios", "Agenda de citas", "Historial médico y vacunas", "Medicamentos y tratamientos", "Facturación y pagos", "Notificaciones a clientes"],
            ["Organizations", "Vets", "Patients", "Owners", "Appointments", "MedicalRecords", "Medications", "Invoices"],
            "Multi-tenant con aislamiento por clínica (OrganizationId en todas las tablas de negocio).",
            "Historial médico requiere trazabilidad rigurosa y backups automáticos. Considerar cumplimiento de privacidad de datos de salud animal.",
            ["Sprint 1: Auth + Tenancy + CRUD Pacientes y Propietarios", "Sprint 2: Agenda de Citas + Recordatorios", "Sprint 3: Historial Médico + Medicamentos", "Sprint 4: Facturación + Dashboard + Deploy Staging"]
        ),

        ["restaurant"] = new(
            "restaurant", "Restaurantes / F&B", "SaaS Operacional",
            "Restaurantes coordinan pedidos, cocina e inventario sin visibilidad centralizada, generando errores de servicio y pérdida de control operacional.",
            "Dueños y gerentes de restaurantes con 5-50 empleados.",
            ["Gestión de mesas", "Pedidos y comandas", "Menú digital", "Display de cocina (KDS)", "Inventario y compras", "Reservaciones", "Reportes de ventas"],
            ["Restaurants", "Tables", "Categories", "MenuItems", "Orders", "OrderItems", "Reservations", "InventoryItems"],
            "Single-tenant por restaurante con módulos especializados por área operacional.",
            "Display de cocina en tiempo real requiere WebSockets. Sincronización de inventario bajo carga alta puede generar race conditions.",
            ["Sprint 1: Auth + Mesas + Menú Digital", "Sprint 2: Pedidos + Comandas + Cocina", "Sprint 3: Inventario + Reservaciones", "Sprint 4: Reportes + POS + Deploy"]
        ),

        ["hr_payroll"] = new(
            "hr_payroll", "RRHH / Planilla", "B2B Multi-tenant",
            "Empresas calculan planilla manualmente o con Excel, generando errores, incumplimientos legales y horas de trabajo administrativo innecesario.",
            "Administradores de RRHH y contadores de empresas con 10-200 empleados.",
            ["Gestión de empleados y departamentos", "Turnos y horarios", "Marcas de asistencia", "Cálculo de horas extra", "Gestión de ausencias y días libres", "Cálculo de planilla quincenal/mensual", "Reportes para CCSS y organismos oficiales"],
            ["Organizations", "Employees", "Departments", "ShiftSchedules", "AttendanceRecords", "PayrollPeriods", "PayrollEntries", "Absences"],
            "Multi-tenant B2B con reglas de negocio parametrizables por organización (legislación, deducciones, beneficios).",
            "Cálculo de planilla varía por empresa, convenios y cambios legislativos. Requiere motor de reglas configurable y auditoría de cambios.",
            ["Sprint 1: Auth + Empleados + Departamentos", "Sprint 2: Turnos + Asistencias + Horas Extra", "Sprint 3: Ausencias + Motor de Planilla", "Sprint 4: Reportes Legales + Dashboard + Deploy"]
        ),

        ["gaming"] = new(
            "gaming", "Casinos / Gaming Operacional", "SaaS Operacional Multi-tenant",
            "Operadores de casinos monitorean máquinas sin visibilidad centralizada, generando tiempos de respuesta lentos ante fallas y pérdida de ingresos.",
            "Operadores de campo y administradores de casinos con múltiples sucursales.",
            ["Gestión de sucursales", "Inventario de máquinas", "Tareas de mantenimiento", "Alertas y monitoreo", "Métricas de rendimiento", "Gestión de operadores"],
            ["Organizations", "Branches", "Machines", "Operators", "Tasks", "MaintenanceLogs", "Alerts", "PerformanceSnapshots"],
            "Multi-tenant operacional con roles diferenciados (admin, supervisor, operador de campo).",
            "Reportes financieros pueden requerir auditoría regulatoria. Alertas en tiempo real idealmente con WebSockets.",
            ["Sprint 1: Auth + Branches + Machines", "Sprint 2: Tasks + Operators + Assignments", "Sprint 3: Alerts + Monitoring Dashboard", "Sprint 4: Reports + Analytics + Deploy"]
        ),

        ["real_estate"] = new(
            "real_estate", "Inmobiliaria / Gestión de Propiedades", "B2B Multi-tenant",
            "Agencias inmobiliarias y administradores de propiedades gestionan contratos, cobros y mantenimiento en hojas de cálculo dispersas.",
            "Agencias inmobiliarias y administradores de propiedades con 5-50 unidades.",
            ["Gestión de propiedades", "Inquilinos y propietarios", "Contratos de arrendamiento", "Cobro de alquileres", "Solicitudes de mantenimiento", "Documentos y firmas"],
            ["Agencies", "Properties", "Owners", "Tenants", "Contracts", "Payments", "MaintenanceRequests", "Documents"],
            "Multi-tenant con aislamiento por agencia. Módulo de documentos requiere storage seguro.",
            "Contratos legales requieren versionado y firma digital. Pagos recurrentes necesitan integración robusta con pasarela de pagos.",
            ["Sprint 1: Auth + Properties + Tenants", "Sprint 2: Contratos + Pagos Recurrentes", "Sprint 3: Mantenimiento + Documentos", "Sprint 4: Dashboard + Reportes + Deploy"]
        ),

        ["healthcare"] = new(
            "healthcare", "Salud / Clínicas", "B2B Multi-tenant",
            "Clínicas y consultorios manejan expedientes y citas en sistemas no integrados, perdiendo trazabilidad clínica y generando riesgos de atención.",
            "Médicos y administradores de clínicas privadas pequeñas a medianas.",
            ["Pacientes y expedientes", "Agenda de citas", "Notas clínicas y diagnósticos", "Prescripciones y medicamentos", "Facturación", "Historia clínica completa"],
            ["Clinics", "Doctors", "Patients", "Appointments", "MedicalRecords", "Prescriptions", "Diagnoses", "Invoices"],
            "Multi-tenant por clínica con separación estricta de datos de pacientes.",
            "Datos de salud requieren encriptación en reposo y en tránsito. Considerar cumplimiento normativo (ley de protección de datos) desde el diseño.",
            ["Sprint 1: Auth + Patients + Doctors", "Sprint 2: Appointments + Medical Records", "Sprint 3: Prescriptions + Billing", "Sprint 4: Dashboard + Compliance + Deploy"]
        ),

        ["education"] = new(
            "education", "Educación / EdTech", "B2B Multi-tenant Institucional",
            "Instituciones educativas coordinan matrículas, cursos y calificaciones en sistemas fragmentados o manuales.",
            "Directores y administradores de centros educativos privados.",
            ["Gestión de estudiantes", "Cursos y materias", "Profesores y asignaciones", "Matrículas", "Calificaciones y boletines", "Pagos de mensualidad", "Comunicados"],
            ["Schools", "Students", "Teachers", "Courses", "Enrollments", "Grades", "Payments", "Announcements"],
            "Multi-tenant institucional con roles bien definidos (admin, docente, tutor, estudiante).",
            "Privacidad de datos de menores requiere manejo especial. Gestión de múltiples períodos académicos necesita diseño cuidadoso del modelo de datos.",
            ["Sprint 1: Auth + Students + Courses + Teachers", "Sprint 2: Enrollments + Grades", "Sprint 3: Payments + Announcements", "Sprint 4: Reports + Parent Portal + Deploy"]
        ),

        ["ecommerce"] = new(
            "ecommerce", "E-commerce / Retail", "B2B/B2C Multi-tenant",
            "Tiendas online gestionan inventario, pedidos y clientes sin centralización, perdiendo ventas por stockouts y mala experiencia de compra.",
            "Emprendedores y dueños de tiendas con catálogos de 50-5000 productos.",
            ["Catálogo de productos", "Gestión de inventario", "Pedidos y fulfillment", "Clientes y direcciones", "Pasarela de pagos", "Envíos y tracking", "Dashboard de ventas"],
            ["Stores", "Products", "Categories", "InventoryItems", "Orders", "OrderItems", "Customers", "Shipments"],
            "Multi-tenant con store por organización. Arquitectura preparada para marketplace futuro.",
            "Sincronización de inventario bajo carga concurrente puede generar overselling. Requerir transacciones optimistas con manejo de conflictos.",
            ["Sprint 1: Auth + Products + Inventory", "Sprint 2: Orders + Payments + Checkout", "Sprint 3: Customers + Shipping + Notifications", "Sprint 4: Analytics + Dashboard + Deploy"]
        ),

        ["logistics"] = new(
            "logistics", "Logística / Supply Chain", "SaaS Operacional",
            "Empresas logísticas no tienen visibilidad centralizada de envíos, rutas y transportistas, generando retrasos y pérdida de trazabilidad.",
            "Gerentes de operaciones logísticas y coordinadores de flota.",
            ["Gestión de bodegas", "Conductores y vehículos", "Envíos y manifiestos", "Rutas y planificación", "Clientes y destinatarios", "Tracking de entregas", "Reportes de KPIs"],
            ["Companies", "Warehouses", "Drivers", "Vehicles", "Shipments", "Routes", "Clients", "TrackingEvents"],
            "Operacional con dashboard en tiempo real. Integración GPS para tracking en sprints futuros.",
            "Tracking en tiempo real requiere integración con GPS/telemática. Optimización de rutas es un problema NP-hard — empezar con asignación manual.",
            ["Sprint 1: Auth + Warehouses + Drivers + Vehicles", "Sprint 2: Shipments + Routes + Assignment", "Sprint 3: Tracking + Client Portal", "Sprint 4: KPIs Dashboard + Analytics + Deploy"]
        ),

        ["general"] = new(
            "general", "SaaS General", "B2B Multi-tenant",
            "Operaciones de negocio dispersas sin sistema centralizado de gestión y visibilidad.",
            "Dueños y equipos de empresas medianas que necesitan centralizar operaciones.",
            ["Gestión de usuarios y organizaciones", "Recursos y entidades principales del negocio", "Dashboard y métricas clave", "Reportes exportables", "Notificaciones y alertas"],
            ["Organizations", "Users", "Resources", "Events", "Settings", "Reports"],
            "Multi-tenant B2B genérico. Modelo de datos a refinar con más contexto del negocio.",
            "Sin industria específica detectada — las heurísticas de arquitectura son genéricas. Se recomienda detallar más el dominio en mensajes siguientes.",
            ["Sprint 1: Auth + Core Entity CRUD", "Sprint 2: Business Logic + Workflows", "Sprint 3: Dashboard + Reporting", "Sprint 4: Notifications + Polish + Deploy"]
        ),
    };

    public static ProductProfile Analyze(string name, string prompt)
    {
        var text = $"{name} {prompt}".ToLowerInvariant();
        var industry = DetectIndustry(text);
        return IndustryProfiles.TryGetValue(industry, out var profile) ? profile : IndustryProfiles["general"];
    }

    private static string DetectIndustry(string text)
    {
        if (ContainsAny(text, "veterinari", "mascota", "animal", "perro", "gato", "felino", "canino")) return "veterinary";
        if (ContainsAny(text, "restaurant", "cocina", "menú", "comida", "platos", "mesero", "chef", "pedido", "mesa")) return "restaurant";
        if (ContainsAny(text, "planilla", "nómina", "quincena", "empleado", "personal", "rrhh", "recursos humanos", "asistencia", "turno", "horas extra")) return "hr_payroll";
        if (ContainsAny(text, "casino", "máquina", "tragamoneda", "slot", "gaming", "juego")) return "gaming";
        if (ContainsAny(text, "inmobili", "propiedad", "arriendo", "alquiler", "inquilino", "arrendatario")) return "real_estate";
        if (ContainsAny(text, "salud", "médico", "clínica", "paciente", "diagnóstico", "receta", "hospital", "consulta")) return "healthcare";
        if (ContainsAny(text, "educación", "escuela", "colegio", "estudiante", "alumno", "curso", "materia", "calificación", "matrícula")) return "education";
        if (ContainsAny(text, "tienda", "ecommerce", "inventario", "producto", "venta", "compra", "catálogo", "carrito")) return "ecommerce";
        if (ContainsAny(text, "logística", "envío", "entrega", "ruta", "bodega", "transportista", "flete", "despacho")) return "logistics";
        return "general";
    }

    private static bool ContainsAny(string text, params string[] terms)
        => terms.Any(text.Contains);

    // ── Content generation methods ──

    public static string GenerateBrief(string name, ProductProfile p) => $"""
        ## Brief del Producto: {name}

        **Industria**: {p.IndustryLabel}
        **Tipo de SaaS**: {p.SaasType}

        ### Problema que resuelve
        {p.Problem}

        ### Usuario objetivo
        {p.TargetUser}

        ### Features core detectadas
        {string.Join("\n", p.CoreFeatures.Select((f, i) => $"{i + 1}. {f}"))}

        ### Propuesta de valor
        {name} centraliza y automatiza las operaciones de {p.IndustryLabel.ToLower()}, reduciendo errores manuales y dando visibilidad en tiempo real a propietarios y equipos.
        """;

    public static string GenerateArchitecture(string name, ProductProfile p) => $"""
        ## Arquitectura del Sistema: {name}

        **Stack**: .NET 9 + SQL Server + Next.js 15 + Azure
        **Patrón**: {p.ArchitecturePattern}

        ### Módulos principales
        {string.Join("\n", p.CoreFeatures.Select((f, i) => $"{i + 1}. **{f.Split(" ").First()}**: {f}"))}

        ### Esquema de base de datos (entidades principales)
        {string.Join(" → ", p.DbEntities)}

        ### Riesgos técnicos identificados
        {p.TechnicalRisk}

        ### Stack validado
        Basado en 3 productos construidos previamente en esta plataforma. Patrones reutilizables disponibles en el sistema de memoria.
        """;

    public static string GenerateRoadmap(ProductProfile p) => $"""
        ## Roadmap MVP

        {string.Join("\n", p.SprintPlan.Select((s, i) => $"**{s.Split(":")[0]}** ({(i == 0 ? "Semana 1-2" : i == 1 ? "Semana 3-4" : i == 2 ? "Semana 5-6" : "Semana 7-8")}): {(s.Contains(":") ? s[(s.IndexOf(':') + 2)..] : s)}"))}

        **Estimado MVP**: 6-8 semanas
        **Deploy staging**: Al finalizar Sprint 4
        **Features post-MVP**: Analytics avanzado, integraciones externas, app móvil
        """;

    public static string GenerateRuntimeChatBrief(string name, ProductProfile p) =>
        $"Analicé tu idea de **{name}**. Detecté una solución de {p.IndustryLabel} con patrón {p.SaasType}. " +
        $"Features core identificadas: {string.Join(", ", p.CoreFeatures.Take(3))}. " +
        $"Generé el brief completo. Continuando con arquitectura del sistema...";

    public static string GenerateRuntimeChatArchitecture(string name, ProductProfile p) =>
        $"Arquitectura definida para **{name}**. " +
        $"El sistema seguirá patrón **{p.ArchitecturePattern}** con {p.DbEntities.Length} entidades principales: " +
        $"{string.Join(", ", p.DbEntities.Take(5))}. " +
        $"Riesgo identificado: {p.TechnicalRisk.Split('.').First()}. " +
        "Necesito tu revisión y aprobación antes de continuar con el sprint planning.";

    public static string GenerateRuntimeChatRoadmap(ProductProfile p) =>
        $"Sprint planning completado. Generé **{p.SprintPlan.Length} sprints**. " +
        $"{p.SprintPlan[0]}. " +
        "Revisá el roadmap y aprobá antes de que inicie la construcción.";

    public static string GenerateRuntimeChatBuilding(string name) =>
        $"Roadmap aprobado. Iniciando **{name}** — Sprint 1. " +
        "El runtime está ejecutando el primer ciclo de construcción. " +
        "Recibirás actualizaciones de progreso en el feed de actividad.";

    public static string GenerateRuntimeChatScaffolding(string name, ProductProfile p) =>
        $"Generando scaffold real de **{name}**.\n\n" +
        $"Voy a crear la estructura base del proyecto: {p.DbEntities.Length} entidades de dominio, " +
        $"controllers para los módulos principales, AppDbContext configurado, y el frontend con Next.js 15 y el Operational Dark design system.\n\n" +
        $"El proyecto va a ser compilable desde el primer commit. Esto puede tomar unos segundos...";

    public static string GenerateRuntimeChatScaffoldComplete(string name, int fileCount, string projectPath) =>
        $"✓ Scaffold de **{name}** completado.\n\n" +
        $"Se generaron **{fileCount} archivos** en `{projectPath}`.\n\n" +
        $"El proyecto incluye: entidades de dominio con campos reales, controllers base, AppDbContext configurado, frontend Next.js con sidebar y páginas por módulo, y el Operational Dark design system aplicado automáticamente.\n\n" +
        $"Podés abrir el proyecto en tu editor y empezar a construir sobre esta base.";

    public static string GenerateFeatureResponse(string feature, ProductProfile p) =>
        $"Registrado el requerimiento de **{feature.Trim()}**. " +
        $"Lo incorporaré en el siguiente sprint. " +
        $"En el contexto de {p.IndustryLabel}, esto se integra con: {string.Join(", ", p.DbEntities.Take(3))}. " +
        "Evaluando impacto en módulos existentes antes de planificar implementación.";

    public static string GenerateFeatureExecutionStart(string featureName, string productName) =>
        $"Ejecutando feature **{featureName}** para {productName}.\n\n" +
        "Generando módulos de forma incremental: entidad de dominio, controller REST, y página frontend con Operational Dark.\n\n" +
        "Solo se crean archivos nuevos — lo que ya existe no se toca.";

    public static string GenerateFeatureExecutionComplete(string featureName, string productName, int created, int skipped) =>
        $"✓ Feature **{featureName}** generada.\n\n" +
        (created > 0
            ? $"**{created}** archivo(s) nuevo(s) creado(s) en el proyecto de {productName}."
            : $"El módulo ya existía en el proyecto — no se sobreescribieron archivos.") +
        (skipped > 0 ? $"\n{skipped} archivo(s) ya existían y fueron saltados (sin cambios)." : "") +
        "\n\nPodés abrir el proyecto y agregar la lógica de negocio en los archivos generados.";

    public static string GenerateDeployResponse() =>
        "Detecté una solicitud de **deploy**. " +
        "Antes de proceder necesito verificar: build limpio, tests pasando y aprobación explícita. " +
        "Creé una solicitud de aprobación de deploy. Confirmala en la sección de Aprobaciones.";

    public static string GenerateBugFixResponse(string text) =>
        $"Recibí el reporte de problema: *\"{text.Trim()[..Math.Min(text.Length, 80)]}\"*. " +
        "Analizando el contexto del error. " +
        "El runtime priorizará este fix en el próximo ciclo. Generaré un diagnóstico detallado.";

    public static string GenerateUiResponse() =>
        "Solicitud de ajuste de UI registrada. " +
        "El runtime revisará los componentes afectados y aplicará los cambios en el próximo sprint de UI. " +
        "Los cambios visuales se reflejarán en el preview link cuando estén listos.";

    public static string GenerateUnknownResponse(string currentStatus) =>
        currentStatus switch
        {
            "Building"  => "Recibido. El Builder OS está en construcción activa. Podés pedir features, reportar bugs o solicitar cambios de UI — te respondo en el próximo ciclo.",
            "Stable"    => "El producto está estable. Podés iniciar un nuevo ciclo de evolución con una solicitud de feature o mejora.",
            "Reviewing" => "Estoy en fase de revisión. Pronto recibirás el resultado. Si hay algo urgente, indicámelo.",
            _           => "Entendido. El Builder OS procesará esta instrucción en el próximo ciclo de construcción.",
        };

    public static string GenerateFeatureBundleComplete(
        string featureName, string productName, int codeFilesCreated, bool navAdded, bool widgetAdded)
    {
        var parts = new List<string>();
        if (codeFilesCreated > 0) parts.Add("entidad, controller y página frontend");
        if (navAdded)             parts.Add("ítem de navegación");
        if (widgetAdded)          parts.Add("widget de dashboard");

        var summary = parts.Count > 0
            ? string.Join(", ", parts)
            : "el módulo ya existía — ningún archivo fue sobreescrito";

        return $"Agregué **{featureName}** como módulo completo en {productName}.\n\n" +
               (codeFilesCreated > 0 ? $"✓ {summary}.\n\n" : $"ℹ {summary}.\n\n") +
               "El módulo ya aparece en el registry del proyecto. Podés agregar la lógica de negocio en los archivos generados.";
    }

    public static string GenerateDashboardUpdateStart(string widgetName, string productName) =>
        $"Procesando solicitud de dashboard para **{productName}**.\n\n" +
        $"Verificando registro del dashboard y buscando widget existente: **{widgetName}**...";

    public static string GenerateDashboardUpdateComplete(string widgetName, string productName, string componentFile) =>
        $"✓ Widget **{widgetName}** agregado al dashboard de {productName}.\n\n" +
        $"Componente generado: `{componentFile}`\n" +
        "El dashboard registry fue actualizado — podés conectar la fuente de datos en el componente generado.\n\n" +
        "Revisá el tab **Cambios** para ver el archivo creado.";

    public static string GenerateDashboardWidgetExists(string widgetName, string productName) =>
        $"El widget **{widgetName}** ya existe en el dashboard de {productName}.\n\n" +
        "No se sobreescribió el componente existente. Si querés modificarlo, editá directamente el archivo en `frontend/components/widgets/`.";

    public static string GenerateDashboardNoDashboard(string productName) =>
        $"No encontré un dashboard principal registrado en el proyecto {productName}.\n\n" +
        "El dashboard se genera automáticamente durante el scaffold. Si el scaffold está completo, " +
        "revisá que exista `frontend/registry/dashboard.json` con `hasMainDashboard: true`.";

    public static string GenerateProjectScanned(string productName, int moduleCount) =>
        $"✓ Escaneé el proyecto **{productName}** y registré **{moduleCount}** módulo(s) en el registro de estructura.\n\n" +
        "El registry está disponible en `frontend/registry/modules.json`. " +
        "Podés ver la estructura completa en el tab **Estructura** del workspace.";

    public static string GenerateFeatureAwareStart(string featureName, string productName) =>
        $"Detecté que **{featureName}** ya existe en el registro del proyecto {productName}.\n\n" +
        "Los archivos de este módulo ya fueron generados — no se sobreescribirán. " +
        "Si querés agregar lógica diferente, editá directamente los archivos existentes.";

    public static string GenerateUiEvolutionStart(string request) =>
        $"Analizando solicitud de evolución de UI: **{request[..Math.Min(request.Length, 60)]}**…\n\n" +
        "El runtime inspeccionará los archivos managed y aplicará el patch si es seguro.";

    public static string GeneratePatchComplete(string patchMessage, bool previewRestarted) =>
        $"✓ {patchMessage}\n\n" +
        (previewRestarted
            ? "Preview reiniciado automáticamente — los cambios se reflejan en el preview activo."
            : "Para ver los cambios, reiniciá el preview desde el tab **Preview**.");

    public static string GeneratePatchSkipped(string reason) =>
        $"ℹ El runtime inspeccionó los archivos managed pero no aplicó cambios.\n\n**Motivo**: {reason}";

    public static string GeneratePatchFailed(string error) =>
        $"⚠ El patch fue abortado por seguridad del runtime.\n\n**Error**: {error}\n\n" +
        "Ningún archivo fue modificado. El proyecto permanece sin cambios.";

    public static string GenerateValidationStart() =>
        "Iniciando validación del proyecto.\n\n" +
        "Ejecutando quality gates: **registry**, **runtime** y **build**. " +
        "Revisá el tab **Calidad** para seguir el progreso en tiempo real.";

    public static string GenerateValidationPassed(int gatesPassed, int autofixAttempts) =>
        $"✓ Validación completa — **{gatesPassed} gate(s)** pasaron.\n\n" +
        (autofixAttempts > 0
            ? $"El runtime detectó y corrigió **{autofixAttempts}** problema(s) automáticamente.\n\n"
            : "") +
        "El proyecto está **saludable** — registry, preview y build son correctos.";

    public static string GenerateValidationFailed(int gatesFailed, int autofixAttempts, IEnumerable<string> autofixLog) =>
        $"⚠ Validación finalizada — **{gatesFailed} gate(s)** fallaron.\n\n" +
        (autofixAttempts > 0
            ? $"Se aplicaron **{autofixAttempts}** ronda(s) de autofix, pero algunos problemas persisten.\n\n"
            : "") +
        "Revisá el tab **Calidad** para ver los gates fallidos y los logs de diagnóstico.\n\n" +
        (autofixLog.Any()
            ? "**Autofix log:**\n" + string.Join("\n", autofixLog.Select(l => $"- {l}"))
            : "");
}
