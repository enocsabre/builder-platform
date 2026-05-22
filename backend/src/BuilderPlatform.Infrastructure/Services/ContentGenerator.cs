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

public record DashboardKpi(string Label, string Value, string Trend, string TrendColor);
public record ActivityRow(string Description, string When, string Status, string StatusColor);
public record DomainContext(
    DashboardKpi[] Kpis,
    ActivityRow[] RecentActivity,
    Dictionary<string, string> EntityLabels,
    string[] QuickActions
);

public record ModuleRow(string[] Cells, string StatusColor);
public record WorkflowTransition(string From, string To, string Label, string ActionColor);
public record ModuleTemplate(
    string                Title,
    string                ActionLabel,
    string                KpiBar,
    string[]              Columns,
    int                   StatusColumnIndex,
    ModuleRow[]           Rows,
    WorkflowTransition[]? Transitions = null
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
        $"El proyecto incluye autenticación básica lista para usar y persistencia local de datos en cada módulo — " +
        $"podés iniciar sesión, agregar registros reales y recargar la página sin perder nada.\n\n" +
        $"Esto puede tomar unos segundos...";

    public static string GenerateRuntimeChatScaffoldComplete(string name, int fileCount, string projectPath) =>
        $"✓ Scaffold de **{name}** completado — **{fileCount} archivos** generados.\n\n" +
        $"**Lo que incluye este scaffold:**\n" +
        $"→ Login con icono de app, badge DEMO y credenciales visibles en pantalla\n" +
        $"→ Sesión protegida con cookie httpOnly — el dashboard requiere login\n" +
        $"→ Módulos domain-aware con datos operacionales reales del dominio\n" +
        $"→ Formularios con validación, campo requerido marcado y placeholder contextual\n" +
        $"→ Toast de confirmación al crear registros (3 s auto-dismiss)\n" +
        $"→ Empty state con ícono y CTA cuando no hay datos\n" +
        $"→ Hover en filas de tabla y separador visual datos demo / datos reales\n" +
        $"→ Sidebar con accent activo y hover states\n" +
        $"→ Los datos sobreviven refresh — no es fake data\n" +
        $"→ Botones de transición de estado en módulos operacionales (ej. Preparar → Completar)\n" +
        $"→ Motor de workflow persistido: cada cambio de estado queda registrado\n" +
        $"→ **Dashboard con KPIs en vivo** — calculados desde registros reales (ventas, órdenes activas, mesas ocupadas, stock bajo)\n" +
        $"→ Badge \"DATOS EN VIVO\" y dot verde en cada card cuando hay datos reales\n" +
        $"→ Auto-refresh cada 30 s + refresh al volver a la pestaña — el sistema siempre muestra estado actual\n" +
        $"→ Última actividad con timestamp relativo en el header del dashboard\n\n" +
        $"Flujo de prueba: login → creá una orden → cambiá su estado → observá cómo las métricas del dashboard cambian automáticamente.";

    public static string GenerateFeatureResponse(string feature, ProductProfile p) =>
        $"Módulo detectado: **{feature.Trim()}**.\n\n" +
        $"En el contexto de {p.IndustryLabel} se integrará con: {string.Join(", ", p.DbEntities.Take(3))}.\n\n" +
        "Generá el scaffold primero para ejecutar el bundle en tiempo real.";

    public static string GenerateFeatureExecutionStart(string featureName, string productName) =>
        $"Detecté el módulo **{featureName}** para {productName}.\n\n" +
        "Generando el feature bundle:\n" +
        "→ Entidad de dominio (.cs)\n" +
        "→ Controller REST (.cs)\n" +
        "→ Página frontend (page.tsx)\n" +
        "→ Ítem en sidebar (nav-items.json)\n" +
        "→ Widget de dashboard\n\n" +
        "Solo se crean archivos nuevos — nada existente se toca.";

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
        "Usá el tab **Deploy** del workspace para iniciar el proceso con confirmación explícita.";

    public static string GenerateDeployStart() =>
        "Iniciando pipeline de **deploy**.\n\n" +
        "Ejecutando pre-deploy quality gates: registry, runtime, build y `next build`.\n\n" +
        "Esto puede tomar hasta 5 minutos. Seguí el progreso en el tab **Deploy**.";

    public static string GenerateDeploySuccess(string? url) =>
        (url is not null
            ? $"✓ **Deploy exitoso.**\n\nTu SaaS está publicado en: **{url}**\n\nPodés compartir ese link — es la URL de producción."
            : "✓ **Deploy exitoso.**\n\nEl deploy se completó correctamente. Revisá el tab **Deploy** para ver la URL de producción.");

    public static string GenerateDeployFailed(string reason) =>
        $"⚠ **Deploy fallido.**\n\n**Motivo**: {reason}\n\n" +
        "Revisá los logs en el tab **Deploy**. Resolvé los problemas y volvé a intentar.";

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

    // ── Domain context for AI-generated dashboard content ─────────────────────

    public static DomainContext GetDomainContext(string industry) => industry switch
    {
        "restaurant" => new(
            Kpis: [
                new("Ventas del día",    "₡2,340,500", "↑ 12% vs ayer",      "var(--status-active-text)"),
                new("Órdenes activas",   "23",          "7 en cocina ahora",  "var(--status-warn-text)"),
                new("Mesas ocupadas",    "8 / 12",      "4 disponibles",      "var(--foreground-muted)"),
                new("Ítems de menú",     "47",          "3 sin stock hoy",    "var(--status-danger-text)"),
            ],
            RecentActivity: [
                new("Mesa 4 — Casado Típico x 2",          "hace 3 min",  "Preparando", "warn"),
                new("Mesa 7 solicitó la cuenta",            "hace 8 min",  "Pendiente",  "info"),
                new("Orden #1247 entregada · Mesa 3",       "hace 15 min", "Completado", "active"),
                new("Mesa 2 — reservación confirmada",      "hace 22 min", "Confirmado", "active"),
                new("Inventario: Arroz bajo stock",         "hace 1 hora", "Alerta",     "danger"),
            ],
            EntityLabels: new() {
                ["Restaurants"] = "Sucursales",  ["Tables"]         = "Mesas",
                ["Categories"]  = "Categorías", ["MenuItems"]      = "Menú",
                ["Orders"]      = "Órdenes",     ["OrderItems"]     = "Comandas",
                ["Reservations"]= "Reservaciones",["InventoryItems"]= "Inventario",
            },
            QuickActions: ["Nueva orden", "Ver mesas", "Actualizar menú"]
        ),

        "veterinary" => new(
            Kpis: [
                new("Citas hoy",         "12",        "↑ 3 vs ayer",         "var(--status-active-text)"),
                new("Pacientes activos", "847",       "12 nuevos este mes",  "var(--status-info-text)"),
                new("Pendiente cobro",   "₡185,000", "3 facturas abiertas", "var(--status-warn-text)"),
                new("Vacunas aplicadas", "5",         "hoy",                 "var(--foreground-muted)"),
            ],
            RecentActivity: [
                new("Firulais (Border Collie) — revisión general", "hace 5 min",  "Completado", "active"),
                new("Michi — vacuna antirrábica aplicada",          "hace 18 min", "Completado", "active"),
                new("Cita 14:30 · Persa Nube — confirmada",         "hace 30 min", "Confirmado", "info"),
                new("Rocky — pendiente resultado de laboratorio",   "hace 2 horas","Pendiente",  "warn"),
                new("Factura #892 · Familia Arias por cobrar",      "hace 3 horas","Pendiente",  "warn"),
            ],
            EntityLabels: new() {
                ["Organizations"] = "Clínicas",      ["Vets"]           = "Veterinarios",
                ["Patients"]      = "Pacientes",     ["Owners"]         = "Propietarios",
                ["Appointments"]  = "Citas",         ["MedicalRecords"] = "Historial",
                ["Medications"]   = "Medicamentos",  ["Invoices"]       = "Facturas",
            },
            QuickActions: ["Nueva cita", "Registrar paciente", "Ver historial"]
        ),

        "hr_payroll" => new(
            Kpis: [
                new("Empleados activos",  "48",           "↑ 2 nuevos este mes",    "var(--status-active-text)"),
                new("Presentes hoy",      "42 / 48",      "6 ausencias registradas", "var(--status-warn-text)"),
                new("Horas extra semana", "127 h",        "18 h no justificadas",   "var(--status-danger-text)"),
                new("Próxima planilla",   "₡24,350,000", "en 3 días",              "var(--status-info-text)"),
            ],
            RecentActivity: [
                new("Ana Gómez — marcó entrada · 07:58",          "hace 12 min", "Activo",     "active"),
                new("Juan Pérez — solicitud de vacaciones",         "hace 1 hora", "Pendiente",  "warn"),
                new("Dpto. Cocina — planilla quincenal cerrada",    "hace 3 horas","Completado", "active"),
                new("María López — 3h extra aprobadas",            "ayer",        "Aprobado",   "info"),
                new("Diego Salas — falta injustificada registrada","ayer",        "Alerta",     "danger"),
            ],
            EntityLabels: new() {
                ["Organizations"]     = "Empresas",      ["Employees"]      = "Empleados",
                ["Departments"]       = "Departamentos", ["ShiftSchedules"] = "Turnos",
                ["AttendanceRecords"] = "Asistencias",   ["PayrollPeriods"] = "Planillas",
                ["PayrollEntries"]    = "Líneas",        ["Absences"]       = "Ausencias",
            },
            QuickActions: ["Registrar asistencia", "Calcular planilla", "Ver empleados"]
        ),

        "gaming" => new(
            Kpis: [
                new("Máquinas activas",  "84 / 92", "8 en mantenimiento",  "var(--status-active-text)"),
                new("Alertas abiertas",  "3",        "1 crítica",           "var(--status-danger-text)"),
                new("Tareas pendientes", "11",       "5 vencidas",          "var(--status-warn-text)"),
                new("Técnicos en campo", "6",        "2 en Sucursal Norte", "var(--status-info-text)"),
            ],
            RecentActivity: [
                new("Slot #47 — error de validación · Sucursal Este", "hace 8 min",  "Crítico",    "danger"),
                new("Técnico J. Mora asignado a Sucursal Norte",       "hace 20 min", "En proceso", "info"),
                new("Máquina #31 — mantenimiento completado",          "hace 45 min", "Resuelto",   "active"),
                new("Payout #4821 · ₡85,000 procesado",               "hace 1 hora", "Completado", "active"),
                new("Sucursal Sur — check-in operador 14:00",          "hace 2 horas","Activo",     "active"),
            ],
            EntityLabels: new() {
                ["Organizations"]        = "Organizaciones",  ["Branches"]           = "Sucursales",
                ["Machines"]             = "Máquinas",        ["Operators"]          = "Operadores",
                ["Tasks"]                = "Tareas",          ["MaintenanceLogs"]    = "Mantenimientos",
                ["Alerts"]               = "Alertas",         ["PerformanceSnapshots"]= "Métricas",
            },
            QuickActions: ["Nueva tarea", "Ver alertas", "Asignar técnico"]
        ),

        "real_estate" => new(
            Kpis: [
                new("Propiedades activas", "73",          "↑ 4 este trimestre",    "var(--status-active-text)"),
                new("Vencen este mes",     "4",           "contratos por renovar", "var(--status-warn-text)"),
                new("Cobros pendientes",   "₡6,200,000", "12 inquilinos",         "var(--status-danger-text)"),
                new("En mantenimiento",    "7",           "3 cerrados esta semana","var(--status-info-text)"),
            ],
            RecentActivity: [
                new("Juan Arce — pagó Apto 302 · mayo",       "hace 2 horas","Pagado",      "active"),
                new("Contrato #247 — vence en 15 días",        "hace 4 horas","Por renovar", "warn"),
                new("Mantenimiento · Cañas Sur #8 abierto",    "hace 6 horas","Abierto",     "info"),
                new("Nuevo inquilino · Local Comercial #5",    "ayer",        "Confirmado",  "active"),
                new("María Solano — mora de 30 días",          "ayer",        "Mora",        "danger"),
            ],
            EntityLabels: new() {
                ["Agencies"]            = "Agencias",       ["Properties"] = "Propiedades",
                ["Owners"]              = "Propietarios",   ["Tenants"]    = "Inquilinos",
                ["Contracts"]           = "Contratos",      ["Payments"]   = "Pagos",
                ["MaintenanceRequests"] = "Mantenimiento",  ["Documents"]  = "Documentos",
            },
            QuickActions: ["Nueva propiedad", "Registrar pago", "Ver contratos"]
        ),

        "healthcare" => new(
            Kpis: [
                new("Citas hoy",             "31",          "↑ 5 vs ayer",          "var(--status-active-text)"),
                new("En espera",             "5",           "espera prom. 18 min",  "var(--status-warn-text)"),
                new("Consultas completadas", "18 / 31",     "en progreso",          "var(--status-info-text)"),
                new("Facturas pendientes",   "₡2,850,000", "7 pacientes",          "var(--status-danger-text)"),
            ],
            RecentActivity: [
                new("Dr. Chacón — consulta · María López completada","hace 10 min","Completado",  "active"),
                new("Diagnóstico actualizado · José Rodríguez",        "hace 25 min","Actualizado", "info"),
                new("Prescripción #2847 emitida · Ana Méndez",         "hace 40 min","Emitida",     "active"),
                new("Cita 15:00 · Pedro Vargas — confirmada",          "hace 1 hora","Confirmado",  "info"),
                new("Factura #1053 · ₡45,000 pendiente de pago",      "hace 3 horas","Pendiente",  "warn"),
            ],
            EntityLabels: new() {
                ["Clinics"]        = "Clínicas",     ["Doctors"]       = "Médicos",
                ["Patients"]       = "Pacientes",    ["Appointments"]  = "Citas",
                ["MedicalRecords"] = "Expedientes",  ["Prescriptions"] = "Recetas",
                ["Diagnoses"]      = "Diagnósticos", ["Invoices"]      = "Facturas",
            },
            QuickActions: ["Nueva cita", "Registrar paciente", "Ver expedientes"]
        ),

        "education" => new(
            Kpis: [
                new("Estudiantes activos",  "312",         "↑ 18 este mes",       "var(--status-active-text)"),
                new("Cursos activos",       "24",          "6 nuevos este ciclo",  "var(--status-info-text)"),
                new("Matrículas pendientes","8",           "por procesar",         "var(--status-warn-text)"),
                new("Por cobrar",           "₡4,100,000", "32 estudiantes",       "var(--status-danger-text)"),
            ],
            RecentActivity: [
                new("Laura Sánchez — completó Matemáticas II",    "hace 15 min","Completado", "active"),
                new("3 nuevas matrículas · período 2026-II",       "hace 30 min","Procesado",  "active"),
                new("Prof. Torres — notas de Inglés publicadas",   "hace 1 hora","Publicado",  "info"),
                new("Diego Ramírez — cuota de junio pendiente",    "hace 3 horas","Pendiente", "warn"),
                new("Nuevo comunicado publicado · Dirección",      "ayer",       "Publicado",  "info"),
            ],
            EntityLabels: new() {
                ["Schools"]       = "Centros",        ["Students"]      = "Estudiantes",
                ["Teachers"]      = "Profesores",     ["Courses"]       = "Cursos",
                ["Enrollments"]   = "Matrículas",     ["Grades"]        = "Calificaciones",
                ["Payments"]      = "Cuotas",         ["Announcements"] = "Comunicados",
            },
            QuickActions: ["Nueva matrícula", "Ver calificaciones", "Publicar comunicado"]
        ),

        "ecommerce" => new(
            Kpis: [
                new("Ventas hoy",      "₡1,870,000", "↑ 23% vs ayer",          "var(--status-active-text)"),
                new("Órdenes activas", "34",          "8 por despachar",        "var(--status-info-text)"),
                new("Stock crítico",   "12",          "productos bajo mínimo",  "var(--status-danger-text)"),
                new("Clientes nuevos", "7",           "hoy",                    "var(--status-warn-text)"),
            ],
            RecentActivity: [
                new("Orden #4829 — enviada a Alajuela · Correos CR","hace 5 min",  "En tránsito","info"),
                new("Orden #4828 — pago confirmado",                 "hace 18 min", "Completado", "active"),
                new("Camiseta XL Negra — sin stock",                 "hace 40 min", "Alerta",     "danger"),
                new("Cliente nuevo registrado",                       "hace 1 hora", "Activo",     "active"),
                new("Devolución #312 procesada · ₡12,500",          "hace 2 horas","Reembolsado","warn"),
            ],
            EntityLabels: new() {
                ["Stores"]        = "Tiendas",     ["Products"]      = "Productos",
                ["Categories"]    = "Categorías", ["InventoryItems"] = "Inventario",
                ["Orders"]        = "Órdenes",     ["OrderItems"]    = "Detalles",
                ["Customers"]     = "Clientes",    ["Shipments"]     = "Envíos",
            },
            QuickActions: ["Nueva orden", "Ver productos", "Despachar pedidos"]
        ),

        "logistics" => new(
            Kpis: [
                new("Envíos activos",       "67",     "14 salieron hoy",     "var(--status-active-text)"),
                new("Entregas completadas", "12 / 18","6 en tránsito",       "var(--status-info-text)"),
                new("Vehículos en ruta",    "8",      "2 con retraso",       "var(--status-warn-text)"),
                new("Incidencias abiertas", "2",      "1 en zona San José",  "var(--status-danger-text)"),
            ],
            RecentActivity: [
                new("Ruta CR-045 — entrega confirmada · Heredia", "hace 8 min",  "Entregado",   "active"),
                new("Chofer Mora — reportó retraso · Cartago",    "hace 25 min", "Alerta",      "warn"),
                new("Envío #8821 — cargado y listo para salir",   "hace 45 min", "En tránsito", "info"),
                new("Camión #7 — revisión técnica completada",     "hace 1 hora", "Listo",       "active"),
                new("Incidencia #43 — ruta 32 bloqueada",          "hace 2 horas","Crítico",     "danger"),
            ],
            EntityLabels: new() {
                ["Companies"]  = "Empresas",    ["Warehouses"]    = "Bodegas",
                ["Drivers"]    = "Conductores", ["Vehicles"]      = "Vehículos",
                ["Shipments"]  = "Envíos",      ["Routes"]        = "Rutas",
                ["Clients"]    = "Clientes",    ["TrackingEvents"]= "Seguimiento",
            },
            QuickActions: ["Nuevo envío", "Ver rutas", "Asignar conductor"]
        ),

        _ => new(
            Kpis: [
                new("Recursos activos",    "124",  "↑ 8% este mes",      "var(--status-active-text)"),
                new("Eventos hoy",         "8",    "3 pendientes",       "var(--status-warn-text)"),
                new("Reportes pendientes", "5",    "vencen esta semana", "var(--foreground-muted)"),
                new("Usuarios activos",    "34",   "2 nuevos hoy",       "var(--status-info-text)"),
            ],
            RecentActivity: [
                new("Recurso #12 — actualizado correctamente",  "hace 5 min",  "Activo",     "active"),
                new("Nuevo evento programado para mañana",       "hace 20 min", "Pendiente",  "info"),
                new("Reporte mensual generado y disponible",     "hace 1 hora", "Completado", "active"),
            ],
            EntityLabels: new(),
            QuickActions: ["Nuevo recurso", "Ver reportes"]
        ),
    };

    // ── Module templates for domain-aware feature pages ────────────────────────

    public static ModuleTemplate? GetModuleTemplate(string feature, string route, string industry)
    {
        var f = Norm(feature);
        var r = route.ToLowerInvariant();
        return industry switch
        {
            "restaurant"  => Restaurant(f, r),
            "veterinary"  => Veterinary(f, r),
            "hr_payroll"  => HrPayroll(f, r),
            "gaming"      => Gaming(f, r),
            "real_estate" => RealEstate(f, r),
            "healthcare"  => Healthcare(f, r),
            "education"   => Education(f, r),
            "ecommerce"   => Ecommerce(f, r),
            "logistics"   => Logistics(f, r),
            _             => null,
        };
    }

    private static string Norm(string s) =>
        s.ToLowerInvariant()
         .Replace("á","a").Replace("é","e").Replace("í","i").Replace("ó","o").Replace("ú","u")
         .Replace("ñ","n").Replace("ü","u");

    private static bool MT(string f, string r, params string[] keys) =>
        keys.Any(k => f.Contains(k) || r.Contains(k));

    private static ModuleTemplate? Restaurant(string f, string r)
    {
        if (MT(f, r, "mesa"))
            return new("Mesas", "Nueva mesa", "12 mesas · 8 ocupadas · 4 disponibles",
                ["Mesa", "Capacidad", "Estado", "Ocupada desde", "Mesero"], 2,
                [
                    new(["Mesa 1", "4 pers.", "Disponible",    "—",            "—"],          "active"),
                    new(["Mesa 2", "4 pers.", "Reservada",     "19:00",        "—"],           "info"),
                    new(["Mesa 3", "2 pers.", "Ocupada",       "hace 45 min",  "Carlos A."],  "warn"),
                    new(["Mesa 4", "6 pers.", "Ocupada",       "hace 1h 12m",  "Sofía M."],   "warn"),
                    new(["Mesa 7", "4 pers.", "Pidió cuenta",  "hace 52 min",  "Diego R."],   "danger"),
                ]);

        if (MT(f, r, "pedido", "comanda"))
            return new("Pedidos", "Nueva orden", "23 activos · ₡124,500 facturado esta hora",
                ["Orden", "Mesa", "Ítems", "Total", "Hora", "Estado"], 5,
                [
                    new(["#1247", "Mesa 4", "Casado Típico x2",            "₡8,500",  "14:23", "Preparando"], "warn"),
                    new(["#1246", "Mesa 7", "Gallo Pinto x1, Jugos x3",   "₡6,200",  "14:15", "En camino"],  "info"),
                    new(["#1245", "Mesa 3", "Arroz con Pollo x2",          "₡12,400", "14:02", "Entregado"],  "active"),
                    new(["#1244", "Mesa 1", "Sopa Negra x1, Refresco x1", "₡4,800",  "13:55", "Cerrado"],    "active"),
                    new(["#1243", "Mesa 2", "Plato del día x3",            "₡9,300",  "13:41", "Cerrado"],    "active"),
                ],
                Transitions:
                [
                    new("Pendiente",   "Preparando",  "Preparar",  "warn"),
                    new("Preparando",  "En camino",   "Completar", "info"),
                    new("En camino",   "Entregado",   "Entregar",  "active"),
                ]);

        if (MT(f, r, "menu", "men"))
            return new("Menú", "Nuevo ítem", "47 ítems · 3 sin stock · 5 categorías",
                ["Nombre", "Categoría", "Precio", "Tiempo prep.", "Disponible"], 4,
                [
                    new(["Casado Típico",   "Platos fuertes", "₡4,250", "15 min", "Disponible"], "active"),
                    new(["Gallo Pinto",     "Desayunos",      "₡2,800", "8 min",  "Disponible"], "active"),
                    new(["Arroz con Leche", "Postres",        "₡1,500", "5 min",  "Disponible"], "active"),
                    new(["Carne Asada",     "Platos fuertes", "₡7,800", "20 min", "Sin stock"],  "danger"),
                    new(["Agua Natural",    "Bebidas",        "₡800",   "1 min",  "Disponible"], "active"),
                ]);

        if (MT(f, r, "display", "kds", "cocina"))
            return new("Cocina (KDS)", "Marcar lista", "7 órdenes en cocina · espera promedio 12 min",
                ["Orden", "Mesa", "Ítems", "Espera", "Prioridad", "Estado"], 5,
                [
                    new(["#1248", "Mesa 5", "Arroz con Pollo x1", "3 min",  "Normal",  "Recibida"],        "info"),
                    new(["#1247", "Mesa 4", "Casado Típico x2",   "8 min",  "Normal",  "En preparación"], "warn"),
                    new(["#1246", "Mesa 7", "Gallo Pinto x1",     "14 min", "Alta",    "En preparación"], "warn"),
                    new(["#1244", "Mesa 3", "Refresco x2",        "22 min", "Urgente", "Lista"],           "active"),
                    new(["#1249", "Mesa 9", "Ceviche x2",         "1 min",  "Normal",  "Recibida"],        "info"),
                ],
                Transitions:
                [
                    new("Recibida",       "En preparación", "Preparar",     "warn"),
                    new("En preparación", "Lista",           "Marcar lista", "active"),
                    new("Lista",          "Entregada",       "Entregar",     "active"),
                ]);

        if (MT(f, r, "inventario"))
            return new("Inventario", "Registrar entrada", "84 productos · 3 bajo mínimo · última compra hace 2 días",
                ["Producto", "Unidad", "Stock", "Mínimo", "Último movimiento", "Estado"], 5,
                [
                    new(["Arroz",           "kg",    "12", "25", "hace 1 hora",  "Bajo stock"], "danger"),
                    new(["Frijoles negros", "kg",    "18", "10", "ayer",         "OK"],          "active"),
                    new(["Aceite vegetal",  "litros","8",  "5",  "hace 2 días",  "OK"],          "active"),
                    new(["Carne molida",    "kg",    "4",  "8",  "hace 3 horas", "Bajo stock"], "warn"),
                    new(["Papas",           "kg",    "22", "10", "ayer",         "OK"],          "active"),
                ]);

        if (MT(f, r, "reserv"))
            return new("Reservaciones", "Nueva reservación", "8 reservaciones hoy · 3 para esta noche",
                ["Cliente", "Personas", "Fecha", "Hora", "Mesa asignada", "Estado"], 5,
                [
                    new(["Ana López",       "4", "2026-05-18", "19:00", "Mesa 6", "Confirmada"],      "active"),
                    new(["Juan Mora",       "2", "2026-05-18", "20:30", "Mesa 2", "Confirmada"],      "active"),
                    new(["Fam. Rodríguez", "6", "2026-05-18", "18:30", "Mesa 8", "Pendiente"],       "warn"),
                    new(["María Salas",    "2", "2026-05-19", "12:00", "Mesa 1", "Confirmada"],      "active"),
                    new(["Carlos Vargas",  "3", "2026-05-17", "20:00", "Mesa 5", "No se presentó"], "danger"),
                ]);

        return null;
    }

    private static ModuleTemplate? Veterinary(string f, string r)
    {
        if (MT(f, r, "paciente"))
            return new("Pacientes", "Nuevo paciente", "847 pacientes · 12 nuevos este mes · 3 en consulta hoy",
                ["Nombre", "Especie", "Raza", "Propietario", "Última visita", "Estado"], 5,
                [
                    new(["Firulais", "Perro", "Border Collie", "Familia Mora",  "hoy",           "En consulta"],         "info"),
                    new(["Michi",    "Gato",  "Persa",         "Ana Arias",     "hace 3 días",   "Activo"],              "active"),
                    new(["Rocky",    "Perro", "Labrador",      "J. Rodríguez",  "hace 1 semana", "Resultado pendiente"], "warn"),
                    new(["Nube",     "Gato",  "Persa",         "C. Fernández",  "hace 2 semanas","Activo"],              "active"),
                    new(["Max",      "Perro", "Beagle",        "M. González",   "hace 1 mes",    "Vacuna pendiente"],    "warn"),
                ]);

        if (MT(f, r, "cita", "agenda"))
            return new("Citas", "Nueva cita", "12 citas hoy · 4 completadas · 2 confirmadas pendientes",
                ["Mascota", "Propietario", "Veterinario", "Tipo", "Hora", "Estado"], 5,
                [
                    new(["Firulais",      "Fam. Mora",    "Dra. Soto", "Revisión general",      "10:00", "Completada"], "active"),
                    new(["Michi",         "Ana Arias",    "Dr. Vega",  "Vacuna antirrábica",    "11:30", "Completada"], "active"),
                    new(["Nube (Persa)",  "C. Fernández", "Dra. Soto", "Consulta · fiebre",    "14:30", "Confirmada"], "info"),
                    new(["Rocky",         "J. Rodríguez", "Dr. Vega",  "Resultado laboratorio", "15:00", "Pendiente"],  "warn"),
                    new(["Max (Beagle)",  "M. González",  "Dra. Soto", "Vacuna anual",          "16:00", "Confirmada"], "info"),
                ]);

        if (MT(f, r, "historial", "histor"))
            return new("Historial médico", "Nuevo registro", "3 registros hoy · 12 esta semana",
                ["Paciente", "Tipo", "Diagnóstico", "Tratamiento", "Fecha", "Veterinario"], -1,
                [
                    new(["Firulais", "Revisión",    "Saludable",          "Control en 6 meses",         "hoy",          "Dra. Soto"], "active"),
                    new(["Michi",    "Vacunación",  "Antirrábica aplicada","—",                         "hoy",          "Dr. Vega"],  "active"),
                    new(["Rocky",    "Laboratorio", "Muestra tomada",     "Resultado pendiente 48h",    "hoy",          "Dr. Vega"],  "warn"),
                    new(["Nube",     "Consulta",    "Dermatitis leve",    "Antibiótico tópico 7 días",  "hace 3 días",  "Dra. Soto"], "active"),
                    new(["Max",      "Vacunación",  "Parvovirus aplicado", "Control anual completado",  "hace 1 sem.",  "Dra. Soto"], "active"),
                ]);

        if (MT(f, r, "medicamento"))
            return new("Medicamentos", "Nuevo medicamento", "142 medicamentos · 3 bajo stock · 5 por vencer",
                ["Medicamento", "Concentración", "Stock", "Unidad", "Vence", "Estado"], 5,
                [
                    new(["Amoxicilina",   "500 mg",   "48 tab",    "Tableta",    "2026-09", "OK"],         "active"),
                    new(["Dexametasona",  "4 mg/ml",  "6 amp",     "Ampolla",    "2026-07", "Stock bajo"],  "warn"),
                    new(["Ivermectina",   "1%",       "12 frascos","Frasco 50ml","2027-01", "OK"],          "active"),
                    new(["Metronidazol",  "250 mg",   "0 tab",     "Tableta",    "2026-11", "Sin stock"],   "danger"),
                    new(["Enrofloxacina", "50 mg/ml", "15 amp",    "Ampolla",    "2026-12", "OK"],          "active"),
                ]);

        if (MT(f, r, "factura", "pago"))
            return new("Facturas", "Nueva factura", "₡185,000 pendiente · 3 facturas abiertas · 28 este mes",
                ["Factura", "Cliente / Mascota", "Concepto", "Total", "Fecha", "Estado"], 5,
                [
                    new(["#892", "Familia Arias / Michi",  "Vacunación + consulta", "₡45,000", "hoy",          "Pendiente"], "warn"),
                    new(["#891", "J. Rodríguez / Rocky",   "Laboratorio",           "₡22,500", "hoy",          "Pendiente"], "warn"),
                    new(["#890", "C. Fernández / Nube",    "Consulta",              "₡18,000", "ayer",         "Pagada"],    "active"),
                    new(["#889", "M. González / Max",      "Vacuna anual",          "₡15,000", "ayer",         "Pagada"],    "active"),
                    new(["#888", "Familia Mora / Firulais", "Revisión",             "₡12,500", "hace 2 días",  "Pagada"],    "active"),
                ]);

        return null;
    }

    private static ModuleTemplate? HrPayroll(string f, string r)
    {
        if (MT(f, r, "empleado"))
            return new("Empleados", "Nuevo empleado", "48 empleados · 42 presentes hoy · 6 ausencias",
                ["Nombre", "Departamento", "Cargo", "Entrada hoy", "Estado"], 4,
                [
                    new(["Ana Gómez",   "Cocina",   "Cocinera jefe", "07:58", "Activo"],   "active"),
                    new(["Juan Pérez",  "Servicio", "Mesero",        "08:05", "Activo"],   "active"),
                    new(["María López", "Admin",    "Cajera",        "09:03", "Tardanza"], "warn"),
                    new(["Diego Salas", "Cocina",   "Ayudante",      "—",     "Falta"],    "danger"),
                    new(["Carla Mora",  "Servicio", "Mesera",        "08:30", "Activo"],   "active"),
                ]);

        if (MT(f, r, "turno"))
            return new("Turnos", "Nuevo turno", "8 turnos activos · próximo cambio: mañana 06:00",
                ["Turno", "Empleados", "Horario", "Días", "Estado"], 4,
                [
                    new(["Turno Mañana · Cocina",   "6 personas",  "06:00 – 14:00", "L-V",      "Activo"],               "active"),
                    new(["Turno Tarde · Servicio",  "8 personas",  "14:00 – 22:00", "L-D",      "Activo"],               "active"),
                    new(["Turno Noche · Cocina",    "3 personas",  "22:00 – 06:00", "V-D",      "Activo"],               "active"),
                    new(["Turno Fin de Semana",     "12 personas", "08:00 – 20:00", "S-D",      "Pendiente asignación"], "warn"),
                    new(["Turno Feriados",          "5 personas",  "09:00 – 18:00", "Feriados", "Próximo: 25 jul"],      "info"),
                ]);

        if (MT(f, r, "asistencia", "marca"))
            return new("Asistencias", "Registrar asistencia", "42 presentes · 6 ausencias · 3 tardanzas hoy",
                ["Empleado", "Departamento", "Entrada", "Salida", "Horas", "Estado"], 5,
                [
                    new(["Ana Gómez",   "Cocina",   "07:58", "—",    "En turno", "Activo"],             "active"),
                    new(["Juan Pérez",  "Servicio", "08:05", "—",    "En turno", "Activo"],             "active"),
                    new(["María López", "Admin",    "09:03", "—",    "En turno", "Tardanza 3 min"],     "warn"),
                    new(["Diego Salas", "Cocina",   "—",     "—",    "—",        "Falta injustificada"],"danger"),
                    new(["Carla Mora",  "Servicio", "08:30", "15:30","7h",        "Salió"],             "info"),
                ]);

        if (MT(f, r, "hora", "extra", "calculo"))
            return new("Horas extra", "Aprobar horas", "127 h esta semana · 18 h pendientes de aprobación",
                ["Empleado", "Fecha", "Horas extra", "Tipo", "Autorizado por", "Estado"], 5,
                [
                    new(["María López", "2026-05-17", "3h",   "Cierre de caja",    "Supervisora C. Mora", "Aprobado"],  "active"),
                    new(["Ana Gómez",   "2026-05-17", "2h",   "Evento especial",   "—",                   "Pendiente"], "warn"),
                    new(["Juan Pérez",  "2026-05-16", "4h",   "Sustitución",       "Supervisora C. Mora", "Aprobado"],  "active"),
                    new(["Diego Salas", "2026-05-15", "1.5h", "Sin justificación", "—",                   "Rechazado"], "danger"),
                    new(["Carla Mora",  "2026-05-14", "2h",   "Inventario mensual","Gerencia",            "Aprobado"],  "active"),
                ]);

        if (MT(f, r, "ausencia", "vacacion", "permiso", "libre"))
            return new("Ausencias", "Registrar ausencia", "3 ausencias hoy · 12 solicitudes pendientes este mes",
                ["Empleado", "Tipo", "Días", "Fecha inicio", "Aprobado por", "Estado"], 5,
                [
                    new(["Diego Salas", "Falta injustificada", "1", "2026-05-18", "—",       "Pendiente"],           "warn"),
                    new(["Juan Pérez",  "Vacaciones",          "5", "2026-05-25", "Gerencia","Aprobado"],            "active"),
                    new(["Ana Gómez",   "Enfermedad",          "2", "2026-05-16", "RRHH",    "Aprobado"],            "active"),
                    new(["Carla Mora",  "Permiso personal",    "1", "2026-05-20", "—",       "Pendiente aprobación"],"warn"),
                    new(["María López", "Feriado",             "1", "2026-07-25", "Legal",   "Programado"],          "info"),
                ]);

        return null;
    }

    private static ModuleTemplate? Gaming(string f, string r)
    {
        if (MT(f, r, "sucursal", "branch"))
            return new("Sucursales", "Nueva sucursal", "6 sucursales activas · 92 máquinas en total",
                ["Sucursal", "Máquinas", "Operadores", "Activas", "Estado"], 4,
                [
                    new(["Sucursal Norte",  "18", "3", "17", "Operativo"],     "active"),
                    new(["Sucursal Sur",    "14", "2", "14", "Operativo"],     "active"),
                    new(["Sucursal Este",   "22", "4", "20", "Alerta activa"], "warn"),
                    new(["Sucursal Oeste",  "16", "3", "15", "Operativo"],     "active"),
                    new(["Sucursal Centro", "22", "4", "18", "Mantenimiento"], "info"),
                ]);

        if (MT(f, r, "maquina", "inventario"))
            return new("Máquinas", "Registrar máquina", "84 activas · 8 en mantenimiento · 3 con alerta",
                ["ID Máquina", "Modelo", "Sucursal", "Último mantenimiento", "Estado"], 4,
                [
                    new(["Slot #47", "IGT S3000",   "Sucursal Este",   "hace 3 días",   "Alerta"],         "danger"),
                    new(["Slot #31", "Aristocrat",  "Sucursal Norte",  "hoy",           "Activa"],          "active"),
                    new(["Slot #15", "Konami",      "Sucursal Sur",    "hace 1 semana", "Activa"],          "active"),
                    new(["Slot #62", "IGT S3000",   "Sucursal Centro", "hace 2 semanas","Mantenimiento"],  "warn"),
                    new(["Slot #78", "Bally Alpha", "Sucursal Oeste",  "ayer",          "Activa"],          "active"),
                ]);

        if (MT(f, r, "tarea"))
            return new("Tareas", "Nueva tarea", "11 pendientes · 5 vencidas · 6 técnicos asignados",
                ["Tarea", "Máquina", "Asignado a", "Fecha límite", "Estado"], 4,
                [
                    new(["Revisión validador billetes", "Slot #47", "J. Mora",   "hoy",        "Urgente"],    "danger"),
                    new(["Calibración sensor",          "Slot #62", "A. Castro", "2026-05-19", "En proceso"], "info"),
                    new(["Limpieza filtros",            "Slot #31", "J. Mora",   "2026-05-20", "Pendiente"],  "warn"),
                    new(["Actualización firmware",      "Slot #15", "R. Vargas", "2026-05-22", "Pendiente"],  "warn"),
                    new(["Mantenimiento preventivo",    "Slot #78", "A. Castro", "2026-05-25", "Programado"], "active"),
                ]);

        if (MT(f, r, "alerta"))
            return new("Alertas", "Nueva alerta", "3 alertas abiertas · 1 crítica · 18 resueltas esta semana",
                ["Tipo", "Máquina / Sucursal", "Descripción", "Creada", "Estado"], 4,
                [
                    new(["Error validador", "Slot #47 · Sucursal Este",  "Falla en validación de billetes",  "hace 8 min",  "Crítico"],  "danger"),
                    new(["Temperatura alta","Slot #62 · Sucursal Centro","Temperatura gabinete > 65°C",      "hace 2 horas","Activa"],   "warn"),
                    new(["Bajo inventario", "Sucursal Norte",             "Tickets de premios < 500 unid.",  "hace 4 horas","Activa"],   "warn"),
                    new(["Puerta abierta",  "Slot #31 · Sucursal Norte", "Puerta trasera abierta 3 min",    "ayer",        "Resuelta"], "active"),
                    new(["Payout alto",     "Slot #15 · Sucursal Sur",   "RTP > 98% en últimas 24h",        "ayer",        "Resuelta"], "active"),
                ]);

        if (MT(f, r, "metrica", "rendimiento", "performance"))
            return new("Métricas", "Exportar reporte", "Rendimiento semanal · actualizado cada 4h",
                ["Sucursal", "Ingresos", "Jackpots", "Tiempo activo", "RTP prom."], -1,
                [
                    new(["Sucursal Norte",  "₡12,340,000", "3", "98.2%", "87.4%"], "active"),
                    new(["Sucursal Sur",    "₡9,180,000",  "1", "97.8%", "88.1%"], "active"),
                    new(["Sucursal Este",   "₡7,240,000",  "2", "94.1%", "89.3%"], "active"),
                    new(["Sucursal Oeste",  "₡11,600,000", "4", "98.5%", "86.8%"], "active"),
                    new(["Sucursal Centro", "₡8,900,000",  "2", "91.4%", "90.1%"], "active"),
                ]);

        return null;
    }

    private static ModuleTemplate? RealEstate(string f, string r)
    {
        if (MT(f, r, "propiedad"))
            return new("Propiedades", "Nueva propiedad", "73 propiedades activas · 4 contratos por vencer este mes",
                ["Código", "Tipo", "Dirección", "Inquilino", "Alquiler mensual", "Estado"], 5,
                [
                    new(["APTO-302", "Apartamento",   "Sabana Norte #8",     "Juan Arce",   "₡480,000", "Ocupado"],        "active"),
                    new(["LOCAL-05", "Local comercial","Cañas Sur",           "—",           "₡350,000", "Disponible"],     "info"),
                    new(["CASA-12",  "Residencia",    "Escazú Central",      "M. Solano",   "₡650,000", "Ocupado"],        "active"),
                    new(["BODEGA-3", "Bodega",        "La Uruca Industrial", "—",           "₡420,000", "En remodelación"],"warn"),
                    new(["APTO-108", "Apartamento",   "Los Yoses #4",        "Fam. Rojas",  "₡390,000", "Ocupado"],        "active"),
                ]);

        if (MT(f, r, "inquilino", "arrendatario", "tenant"))
            return new("Inquilinos", "Nuevo inquilino", "58 inquilinos activos · 2 en mora · 4 contratos por vencer",
                ["Nombre", "Propiedad", "Contrato vence", "Alquiler", "Saldo", "Estado"], 5,
                [
                    new(["Juan Arce",    "APTO-302",  "2026-12-31", "₡480,000", "₡0",        "Al día"],    "active"),
                    new(["María Solano", "CASA-12",   "2026-08-15", "₡650,000", "₡1,300,000","Mora 2m"],   "danger"),
                    new(["Fam. Rojas",   "APTO-108",  "2027-03-31", "₡390,000", "₡0",        "Al día"],    "active"),
                    new(["C. Jiménez",   "OFICINA-2", "2026-06-30", "₡280,000", "₡0",        "Por renovar"],"warn"),
                    new(["R. Montero",   "LOCAL-07",  "2027-01-15", "₡520,000", "₡0",        "Al día"],    "active"),
                ]);

        if (MT(f, r, "contrato"))
            return new("Contratos", "Nuevo contrato", "61 contratos activos · 4 vencen este mes",
                ["Contrato", "Propiedad", "Inquilino", "Inicio", "Vence", "Estado"], 5,
                [
                    new(["#247", "APTO-302",  "Juan Arce",  "2024-01-01", "2026-12-31", "Vigente"],        "active"),
                    new(["#246", "CASA-12",   "M. Solano",  "2024-02-15", "2026-08-15", "Por vencer"],     "warn"),
                    new(["#243", "OFICINA-2", "C. Jiménez", "2024-06-30", "2026-06-30", "Vence este mes"], "danger"),
                    new(["#238", "APTO-108",  "Fam. Rojas", "2025-04-01", "2027-03-31", "Vigente"],        "active"),
                    new(["#251", "LOCAL-07",  "R. Montero", "2025-01-15", "2027-01-15", "Vigente"],        "active"),
                ]);

        if (MT(f, r, "cobro", "alquiler", "pago"))
            return new("Cobros", "Registrar pago", "₡6,200,000 pendiente · 12 inquilinos · mayo 2026",
                ["Inquilino", "Propiedad", "Monto", "Vence", "Pagado", "Estado"], 5,
                [
                    new(["Juan Arce",    "APTO-302",  "₡480,000", "30 mayo",  "30 mayo", "Pagado"],   "active"),
                    new(["María Solano", "CASA-12",   "₡650,000", "15 mayo",  "—",       "Atrasado"], "danger"),
                    new(["Fam. Rojas",   "APTO-108",  "₡390,000", "01 junio", "—",       "Pendiente"],"warn"),
                    new(["C. Jiménez",   "OFICINA-2", "₡280,000", "30 mayo",  "28 mayo", "Pagado"],   "active"),
                    new(["R. Montero",   "LOCAL-07",  "₡520,000", "15 junio", "—",       "Pendiente"],"info"),
                ]);

        if (MT(f, r, "mantenimiento", "solicitud"))
            return new("Mantenimiento", "Nueva solicitud", "7 solicitudes activas · 3 cerradas esta semana",
                ["Solicitud", "Propiedad", "Descripción", "Prioridad", "Asignado a", "Estado"], 5,
                [
                    new(["#078", "APTO-302",  "Fuga de agua baño principal","Alta",  "Plomero: J. Castro", "En proceso"],"warn"),
                    new(["#077", "BODEGA-3",  "Puerta eléctrica dañada",   "Media", "Electricista: TBD",  "Abierta"],   "info"),
                    new(["#076", "CASA-12",   "Pintura exterior",           "Baja",  "Contratista: A. Mora","Programada"],"info"),
                    new(["#075", "LOCAL-07",  "A/C no enfría",             "Alta",  "Técnico: R. Soto",   "En proceso"], "warn"),
                    new(["#074", "APTO-108",  "Cerradura cilindro",        "Media", "Cerrajero: L. Arias","Cerrada"],    "active"),
                ]);

        return null;
    }

    private static ModuleTemplate? Healthcare(string f, string r)
    {
        if (MT(f, r, "paciente"))
            return new("Pacientes", "Nuevo paciente", "2,341 pacientes · 31 citas hoy · 5 en espera",
                ["Nombre", "Cédula", "Fecha nac.", "Médico asignado", "Última consulta", "Estado"], 5,
                [
                    new(["María López",    "1-0842-0311", "1985-03-12", "Dr. Chacón", "hoy",           "En consulta"],         "info"),
                    new(["José Rodríguez", "5-0234-5678", "1972-07-24", "Dra. Mora",  "hace 3 días",   "Activo"],              "active"),
                    new(["Ana Méndez",     "3-0567-8901", "1990-11-08", "Dr. Chacón", "hace 1 semana", "Activo"],              "active"),
                    new(["Pedro Vargas",   "7-0123-4567", "1958-01-30", "Dra. Mora",  "hace 2 semanas","Activo"],              "active"),
                    new(["Laura Brenes",   "2-0789-0123", "2001-05-15", "Dr. Chacón", "ayer",          "Resultado pendiente"], "warn"),
                ]);

        if (MT(f, r, "cita", "agenda"))
            return new("Citas", "Nueva cita", "31 citas hoy · 18 completadas · 5 en espera",
                ["Paciente", "Médico", "Motivo", "Hora", "Sala", "Estado"], 5,
                [
                    new(["María López",    "Dr. Chacón", "Consulta general",        "09:00", "Sala 2", "Completada"], "active"),
                    new(["José Rodríguez", "Dra. Mora",  "Seguimiento hipertensión", "10:30", "Sala 1", "Completada"], "active"),
                    new(["Pedro Vargas",   "Dra. Mora",  "Primera consulta",         "15:00", "Sala 1", "Confirmada"], "info"),
                    new(["Laura Brenes",   "Dr. Chacón", "Resultado exámenes",       "16:00", "Sala 3", "Pendiente"],  "warn"),
                    new(["Carmen Solano",  "Dr. Chacón", "Control diabetes",         "16:45", "Sala 2", "Confirmada"], "info"),
                ]);

        if (MT(f, r, "nota", "expediente", "diagnostico", "record"))
            return new("Expedientes", "Nuevo registro", "12 actualizaciones hoy · 847 expedientes activos",
                ["Paciente", "Tipo", "Diagnóstico", "Médico", "Fecha"], -1,
                [
                    new(["María López",    "Consulta",    "Hipertensión arterial controlada",  "Dr. Chacón", "hoy"],           "active"),
                    new(["José Rodríguez", "Seguimiento", "Diabetes tipo 2 — ajuste dosis",   "Dra. Mora",  "hoy"],           "active"),
                    new(["Ana Méndez",     "Prescripción","Anemia ferropénica leve",           "Dr. Chacón", "hace 1 semana"], "active"),
                    new(["Pedro Vargas",   "Laboratorio", "Resultados pendientes 48h",         "Dra. Mora",  "hace 2 semanas"],"active"),
                    new(["Laura Brenes",   "Consulta",    "Gastritis — tratamiento 2 semanas", "Dr. Chacón", "ayer"],          "active"),
                ]);

        if (MT(f, r, "prescripcion", "receta", "medicamento"))
            return new("Prescripciones", "Nueva prescripción", "47 prescripciones este mes · 12 activas",
                ["Paciente", "Medicamento", "Dosis", "Duración", "Médico", "Estado"], 5,
                [
                    new(["Ana Méndez",     "Sulfato ferroso 300mg", "1 tab. c/8h",    "3 meses", "Dr. Chacón","Activa"], "active"),
                    new(["María López",    "Losartán 50mg",         "1 tab. c/24h",   "Crónico", "Dr. Chacón","Activa"], "active"),
                    new(["José Rodríguez", "Metformina 850mg",      "1 tab. c/12h",   "Crónico", "Dra. Mora", "Activa"], "active"),
                    new(["Laura Brenes",   "Omeprazol 20mg",        "1 tab. en ayunas","14 días", "Dr. Chacón","Activa"], "active"),
                    new(["Pedro Vargas",   "Atorvastatina 20mg",    "1 tab. nocturna", "Crónico", "Dra. Mora", "Activa"], "active"),
                ]);

        if (MT(f, r, "factura", "pago", "cobro"))
            return new("Facturas", "Nueva factura", "₡2,850,000 pendiente · 7 pacientes · este mes",
                ["Factura", "Paciente", "Concepto", "Total", "Fecha", "Estado"], 5,
                [
                    new(["#1053", "María López",    "Consulta + exámenes", "₡85,000",  "hoy",          "Pendiente"], "warn"),
                    new(["#1052", "Laura Brenes",   "Consulta",            "₡45,000",  "ayer",         "Pendiente"], "warn"),
                    new(["#1051", "José Rodríguez", "Seguimiento",         "₡35,000",  "ayer",         "Pagada"],    "active"),
                    new(["#1050", "Ana Méndez",     "Laboratorio",         "₡62,000",  "hace 3 días",  "Pagada"],    "active"),
                    new(["#1049", "Pedro Vargas",   "Primera consulta",    "₡45,000",  "hace 1 semana","Pagada"],    "active"),
                ]);

        return null;
    }

    private static ModuleTemplate? Education(string f, string r)
    {
        if (MT(f, r, "estudiante", "alumno"))
            return new("Estudiantes", "Nuevo estudiante", "312 estudiantes activos · 18 nuevos este mes",
                ["Nombre", "Cédula", "Nivel", "Sección", "Saldo pendiente", "Estado"], 5,
                [
                    new(["Laura Sánchez",  "3-0412-5678", "11° año", "11-A", "₡0",       "Al día"],    "active"),
                    new(["Diego Ramírez",  "5-0234-1234", "10° año", "10-B", "₡45,000",  "Mora 1m"],   "warn"),
                    new(["Sofía Mora",     "2-0567-9012", "12° año", "12-A", "₡0",       "Al día"],    "active"),
                    new(["Carlos Herrera", "7-0890-3456", "9° año",  "9-C",  "₡90,000",  "Mora 2m"],   "danger"),
                    new(["Ana Jiménez",    "4-0123-7890", "11° año", "11-B", "₡0",       "Al día"],    "active"),
                ]);

        if (MT(f, r, "curso", "materia"))
            return new("Cursos", "Nuevo curso", "24 cursos activos · 312 estudiantes matriculados",
                ["Curso", "Nivel", "Profesor", "Estudiantes", "Horario", "Estado"], 5,
                [
                    new(["Matemáticas II",  "10° año", "Prof. Torres",  "28", "L-M-V 7:00",  "Activo"], "active"),
                    new(["Inglés Avanzado", "11° año", "Prof. Vega",    "24", "M-J 8:00",    "Activo"], "active"),
                    new(["Física",          "12° año", "Prof. Solis",   "20", "L-M-V 10:00", "Activo"], "active"),
                    new(["Historia CR",     "9° año",  "Prof. Mora",    "32", "L-M-J 11:00", "Activo"], "active"),
                    new(["Química",         "11° año", "Prof. Torres",  "26", "M-V 9:00",    "Activo"], "active"),
                ]);

        if (MT(f, r, "profesor", "docente", "teacher"))
            return new("Profesores", "Nuevo profesor", "48 profesores activos · 24 cursos este período",
                ["Nombre", "Especialidad", "Cursos asignados", "Horas", "Estado"], 4,
                [
                    new(["Prof. Torres",  "Matemáticas",  "3 cursos", "40h/sem", "Activo"],  "active"),
                    new(["Prof. Vega",    "Inglés",       "2 cursos", "32h/sem", "Activo"],  "active"),
                    new(["Prof. Solis",   "Física",       "2 cursos", "30h/sem", "Activo"],  "active"),
                    new(["Prof. Mora",    "Historia",     "3 cursos", "38h/sem", "Activo"],  "active"),
                    new(["Prof. Arias",   "Biología",     "2 cursos", "28h/sem", "Permiso"], "warn"),
                ]);

        if (MT(f, r, "matricula", "matriculas", "enrollment"))
            return new("Matrículas", "Nueva matrícula", "3 matrículas pendientes · período 2026-II",
                ["Estudiante", "Cursos", "Nivel", "Fecha", "Cuota mensual", "Estado"], 5,
                [
                    new(["Laura Sánchez",  "8 materias", "11° año", "2026-01-15", "₡85,000", "Procesada"],  "active"),
                    new(["Diego Ramírez",  "7 materias", "10° año", "2026-01-16", "₡85,000", "Pendiente pago"],"warn"),
                    new(["Sofía Mora",     "9 materias", "12° año", "2026-01-14", "₡85,000", "Procesada"],  "active"),
                    new(["Carlos Herrera", "6 materias", "9° año",  "—",          "₡85,000", "Sin procesar"],"danger"),
                    new(["Ana Jiménez",    "8 materias", "11° año", "2026-01-17", "₡85,000", "Procesada"],  "active"),
                ]);

        if (MT(f, r, "calificacion", "nota", "boletin", "grade"))
            return new("Calificaciones", "Registrar nota", "Período 2026-I · 24 cursos · promedios publicados",
                ["Estudiante", "Curso", "Nota", "Período", "Profesor", "Estado"], 5,
                [
                    new(["Laura Sánchez",  "Matemáticas II",  "92",  "2026-I", "Prof. Torres", "Publicada"], "active"),
                    new(["Laura Sánchez",  "Inglés Avanzado", "88",  "2026-I", "Prof. Vega",   "Publicada"], "active"),
                    new(["Diego Ramírez",  "Matemáticas II",  "61",  "2026-I", "Prof. Torres", "En revisión"],"warn"),
                    new(["Sofía Mora",     "Física",          "95",  "2026-I", "Prof. Solis",  "Publicada"], "active"),
                    new(["Carlos Herrera", "Historia CR",     "54",  "2026-I", "Prof. Mora",   "Aplazado"],  "danger"),
                ]);

        return null;
    }

    private static ModuleTemplate? Ecommerce(string f, string r)
    {
        if (MT(f, r, "catalogo", "producto", "product"))
            return new("Productos", "Nuevo producto", "1,247 productos · 12 sin stock · 8 categorías",
                ["Nombre", "Categoría", "Precio", "Stock", "SKU", "Estado"], 5,
                [
                    new(["Camiseta Polo Negra XL",    "Ropa",       "₡18,500", "48",  "ROП-001-XL", "Disponible"],  "active"),
                    new(["Tenis Running Azul 42",     "Calzado",    "₡65,000", "12",  "CAL-042-AZ", "Disponible"],  "active"),
                    new(["Mochila Escolar 25L",       "Accesorios", "₡24,000", "0",   "ACC-025-MC", "Sin stock"],   "danger"),
                    new(["Reloj Digital Deportivo",   "Accesorios", "₡42,000", "7",   "ACC-001-RD", "Stock bajo"],  "warn"),
                    new(["Pantalón Jeans Slim 32",    "Ropa",       "₡28,000", "35",  "ROП-032-SL", "Disponible"],  "active"),
                ]);

        if (MT(f, r, "inventario"))
            return new("Inventario", "Ajustar stock", "1,247 SKUs · 12 agotados · 23 stock bajo mínimo",
                ["SKU", "Producto", "Stock actual", "Mínimo", "Bodega", "Estado"], 5,
                [
                    new(["ACC-025-MC", "Mochila Escolar 25L",    "0",  "15", "Central", "Sin stock"],   "danger"),
                    new(["ACC-001-RD", "Reloj Digital Deportivo","7",  "10", "Central", "Stock bajo"],   "warn"),
                    new(["ROП-001-XL", "Camiseta Polo Negra XL", "48", "20", "Central", "OK"],           "active"),
                    new(["CAL-042-AZ", "Tenis Running Azul 42",  "12", "8",  "Central", "OK"],           "active"),
                    new(["ROП-032-SL", "Pantalón Jeans Slim 32", "35", "15", "Central", "OK"],           "active"),
                ]);

        if (MT(f, r, "pedido", "orden", "fulfillment", "order"))
            return new("Órdenes", "Nueva orden", "34 activas · 8 por despachar · ₡1,870,000 hoy",
                ["Orden", "Cliente", "Ítems", "Total", "Fecha", "Estado"], 5,
                [
                    new(["#4829", "María López",   "Camiseta XL x2, Jeans x1", "₡65,000",  "hace 5 min",  "En tránsito"], "info"),
                    new(["#4828", "Juan Mora",     "Tenis Azul 42",            "₡65,000",  "hace 18 min", "Completada"], "active"),
                    new(["#4827", "Ana Soto",      "Mochila 25L x1",           "₡24,000",  "hace 1 hora", "Sin stock"],  "danger"),
                    new(["#4826", "Carlos Rivera", "Reloj Deportivo x1",       "₡42,000",  "ayer",        "Despachada"], "active"),
                    new(["#4825", "Laura Quesada", "Pantalón Slim x2",         "₡56,000",  "ayer",        "Completada"], "active"),
                ]);

        if (MT(f, r, "cliente", "customer"))
            return new("Clientes", "Nuevo cliente", "4,821 clientes · 7 nuevos hoy · LTV promedio ₡145,000",
                ["Nombre", "Email", "Órdenes", "Total comprado", "Última compra", "Estado"], 5,
                [
                    new(["María López",   "maria@email.com",   "12", "₡312,000", "hace 5 min",  "Activo"], "active"),
                    new(["Juan Mora",     "juan@email.com",    "8",  "₡198,000", "hace 18 min", "Activo"], "active"),
                    new(["Ana Soto",      "ana@email.com",     "3",  "₡72,000",  "hace 1 hora", "Activo"], "active"),
                    new(["Carlos Rivera", "carlos@email.com",  "21", "₡524,000", "ayer",        "Activo"], "active"),
                    new(["Laura Quesada", "laura@email.com",   "1",  "₡56,000",  "ayer",        "Nuevo"],  "info"),
                ]);

        if (MT(f, r, "envio", "pasarela", "shipment", "pago"))
            return new("Envíos", "Nuevo envío", "14 envíos hoy · 3 con retraso · Correos CR + Fedex",
                ["Orden", "Cliente", "Destino", "Courier", "Fecha estimada", "Estado"], 5,
                [
                    new(["#4829", "María López",   "San José",   "Correos CR",  "2026-05-20", "En tránsito"], "info"),
                    new(["#4826", "Carlos Rivera", "Cartago",    "Fedex CR",    "2026-05-19", "Entregado"],   "active"),
                    new(["#4824", "Sofía Morales", "Heredia",    "Correos CR",  "2026-05-18", "Retrasado"],   "warn"),
                    new(["#4822", "Diego Ureña",   "Guanacaste", "Fedex CR",    "2026-05-21", "En tránsito"], "info"),
                    new(["#4820", "Carmen Salas",  "Alajuela",   "Correos CR",  "2026-05-18", "Entregado"],   "active"),
                ]);

        return null;
    }

    private static ModuleTemplate? Logistics(string f, string r)
    {
        if (MT(f, r, "bodega", "warehouse"))
            return new("Bodegas", "Nueva bodega", "4 bodegas activas · 67 envíos en proceso",
                ["Bodega", "Ubicación", "Capacidad", "Ocupada", "Responsable", "Estado"], 5,
                [
                    new(["Bodega Central", "La Uruca, SJ",   "500 pallets", "78%", "J. Rodríguez", "Operativa"],     "active"),
                    new(["Bodega Norte",   "Alajuela",        "200 pallets", "45%", "A. Castro",    "Operativa"],     "active"),
                    new(["Bodega Sur",     "Desamparados",    "300 pallets", "92%", "M. López",     "Casi llena"],    "warn"),
                    new(["Bodega Carga",   "Aeropuerto SJO",  "100 pallets", "30%", "R. Vargas",    "Operativa"],     "active"),
                    new(["Bodega Fría",    "Cartago",         "80 pallets",  "0%",  "—",            "Mantenimiento"], "info"),
                ]);

        if (MT(f, r, "conductor", "chofer", "driver"))
            return new("Conductores", "Nuevo conductor", "18 conductores activos · 8 en ruta ahora",
                ["Nombre", "Licencia", "Vehículo asignado", "Ruta activa", "Estado"], 4,
                [
                    new(["J. Mora",     "B2 #45-892", "Camión #7",   "CR-045 · Heredia",   "En ruta"],   "active"),
                    new(["A. Castro",   "B1 #32-110", "Van #3",      "CR-023 · Cartago",   "En ruta"],   "active"),
                    new(["M. López",    "B2 #67-234", "Camión #12",  "—",                  "Disponible"],"info"),
                    new(["R. Vargas",   "B1 #89-456", "Van #8",      "CR-012 · Alajuela",  "En ruta"],   "active"),
                    new(["D. Salas",    "B2 #23-789", "Camión #5",   "—",                  "Descanso"],  "warn"),
                ]);

        if (MT(f, r, "envio", "manifiesto", "shipment"))
            return new("Envíos", "Nuevo envío", "67 activos · 14 salieron hoy · 12 entregados",
                ["Guía", "Origen", "Destino", "Conductor", "Estimado", "Estado"], 5,
                [
                    new(["#8821", "Bodega Central", "Heredia",       "J. Mora",   "hoy 16:00",   "En tránsito"], "info"),
                    new(["#8820", "Bodega Norte",   "Guanacaste",    "A. Castro", "mañana 10:00","En tránsito"], "info"),
                    new(["#8819", "Bodega Central", "Cartago",       "R. Vargas", "hoy 15:00",   "Entregado"],   "active"),
                    new(["#8818", "Bodega Sur",     "San José",      "J. Mora",   "ayer",        "Entregado"],   "active"),
                    new(["#8817", "Bodega Carga",   "Limón",         "—",         "mañana",      "Retrasado"],   "warn"),
                ]);

        if (MT(f, r, "ruta", "route"))
            return new("Rutas", "Nueva ruta", "12 rutas activas · 8 vehículos en ruta hoy",
                ["Código", "Origen", "Destino", "Conductor", "Vehículo", "Estado"], 5,
                [
                    new(["CR-045", "Bodega Central", "Heredia",     "J. Mora",   "Camión #7",  "Activa"],     "active"),
                    new(["CR-032", "Bodega Sur",     "Cartago",     "A. Castro", "Camión #12", "Activa"],     "active"),
                    new(["CR-023", "Bodega Norte",   "Guanacaste",  "R. Vargas", "Van #3",     "Activa"],     "active"),
                    new(["CR-012", "Bodega Central", "Limón",       "—",         "—",          "Sin asignar"],"warn"),
                    new(["CR-008", "Bodega Carga",   "San José",    "D. Salas",  "Van #8",     "Completada"], "active"),
                ]);

        if (MT(f, r, "cliente", "destinatario", "client"))
            return new("Clientes", "Nuevo cliente", "234 clientes activos · 18 con envíos esta semana",
                ["Nombre", "RUC / Cédula", "Envíos activos", "Último envío", "Contacto", "Estado"], 5,
                [
                    new(["Distribuidora Norte S.A.", "3-101-123456", "4", "hoy",           "J. Pérez",  "Activo"],  "active"),
                    new(["Supermercado del Río",     "3-102-234567", "2", "ayer",           "A. Mora",   "Activo"],  "active"),
                    new(["Ferretería Central",       "3-103-345678", "1", "hace 3 días",    "R. Soto",   "Activo"],  "active"),
                    new(["Farmacia Vida",            "3-104-456789", "0", "hace 2 semanas", "M. Castro", "Inactivo"],"warn"),
                    new(["Exportadora CR",           "3-105-567890", "7", "hoy",            "L. Arias",  "Activo"],  "active"),
                ]);

        return null;
    }

    // ── Evolution-aware messages (Sprint 25) ──────────────────────────────────

    /// <summary>
    /// Completion message when a feature was generated AND relations were detected.
    /// </summary>
    public static string GenerateEvolutionBundleComplete(
        string featureName, string productName,
        List<EvolutionRelation> relations,
        int codeFilesCreated, bool navAdded, bool widgetAdded)
    {
        if (relations.Count == 0)
            return GenerateFeatureBundleComplete(featureName, productName, codeFilesCreated, navAdded, widgetAdded);

        var connected = relations.Select(r => $"**{r.To}**").Distinct().ToList();
        var connectionText = connected.Count == 1
            ? connected[0]
            : string.Join(", ", connected[..^1]) + " y " + connected[^1];

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Módulo **{featureName}** generado y conectado evolutivamente con {connectionText}.");
        sb.AppendLine();

        // Show at most 2 relation reasons
        foreach (var rel in relations.Take(2))
            sb.AppendLine($"— _{rel.Reason}_");
        sb.AppendLine();

        var parts = new List<string>();
        if (codeFilesCreated > 0) parts.Add($"{codeFilesCreated} archivo(s) generados");
        if (navAdded)              parts.Add("navegación actualizada");
        if (widgetAdded)           parts.Add("widget en dashboard");

        sb.Append(parts.Count > 0 ? string.Join(", ", parts) + ". " : "");
        sb.Append("El producto mantiene coherencia arquitectónica — los flujos están conectados.");

        return sb.ToString();
    }

    /// <summary>
    /// Start message for feature execution when the runtime has evolution context.
    /// </summary>
    public static string GenerateEvolutionAwareStart(
        string featureName, string productName, List<EvolutionRelation> relations)
    {
        if (relations.Count == 0)
            return GenerateFeatureExecutionStart(featureName, productName);

        var connected = relations.Select(r => $"**{r.To}**").Distinct().ToList();
        var connectionText = connected.Count == 1
            ? connected[0]
            : string.Join(", ", connected[..^1]) + " y " + connected[^1];

        return $"Analicé la arquitectura de **{productName}** y detecté que **{featureName}** " +
               $"se conecta con módulos existentes: {connectionText}.\n\n" +
               $"Generando el módulo con contexto evolutivo — los flujos serán coherentes con la arquitectura actual del producto.";
    }
}
