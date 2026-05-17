namespace BuilderPlatform.Infrastructure.Services;

public static class ArtifactContentGenerator
{
    // ── Per-industry DB schemas ──────────────────────────────────────────────

    private static readonly Dictionary<string, string> DbSchemas = new()
    {
        ["veterinary"] = """
            ## organizations (clínicas)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | name | VARCHAR(200) | Nombre de la clínica |
            | slug | VARCHAR(100) | Identificador URL |
            | phone | VARCHAR(20) | |
            | address | VARCHAR(500) | |
            | timezone | VARCHAR(50) | Default: 'America/Costa_Rica' |
            | created_at | DATETIME | |

            ## owners (propietarios de mascotas)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK → organizations |
            | full_name | VARCHAR(200) | |
            | email | VARCHAR(200) | |
            | phone | VARCHAR(20) | |
            | address | VARCHAR(500) | |

            ## patients (mascotas)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK → organizations |
            | owner_id | UUID | FK → owners |
            | name | VARCHAR(100) | Nombre de la mascota |
            | species | VARCHAR(50) | perro, gato, ave, etc. |
            | breed | VARCHAR(100) | Raza |
            | date_of_birth | DATE | |
            | weight_kg | DECIMAL(5,2) | |
            | is_active | BIT | |

            ## appointments (citas)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | patient_id | UUID | FK → patients |
            | vet_id | UUID | FK → employees |
            | scheduled_at | DATETIME | |
            | duration_minutes | INT | Default: 30 |
            | reason | VARCHAR(500) | Motivo de la consulta |
            | status | VARCHAR(20) | pending / confirmed / completed / cancelled |
            | notes | TEXT | |

            ## medical_records (historial médico)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | patient_id | UUID | FK → patients |
            | appointment_id | UUID | FK → appointments (nullable) |
            | diagnosis | TEXT | |
            | treatment | TEXT | |
            | prescription | TEXT | |
            | follow_up_date | DATE | |
            | created_by | UUID | FK → employees |
            | created_at | DATETIME | |

            ## vaccines (vacunas)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | patient_id | UUID | FK → patients |
            | vaccine_name | VARCHAR(200) | |
            | applied_at | DATE | |
            | next_due_at | DATE | |
            | lot_number | VARCHAR(100) | |

            ## invoices (facturas)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | appointment_id | UUID | FK → appointments |
            | total_amount | DECIMAL(10,2) | |
            | paid_at | DATETIME | |
            | payment_method | VARCHAR(20) | cash / card / transfer |
            | status | VARCHAR(20) | pending / paid / cancelled |

            ## Índices recomendados
            - `appointments(organization_id, scheduled_at)` — agenda por fecha
            - `appointments(patient_id, scheduled_at)` — historial de citas por mascota
            - `medical_records(patient_id, created_at)` — historial médico cronológico
            - `vaccines(patient_id, next_due_at)` — alertas de vacunas próximas
            """,

        ["restaurant"] = """
            ## restaurants
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | name | VARCHAR(200) | |
            | slug | VARCHAR(100) | |
            | address | VARCHAR(500) | |
            | phone | VARCHAR(20) | |
            | timezone | VARCHAR(50) | |

            ## tables (mesas)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | restaurant_id | UUID | FK → restaurants |
            | number | INT | Número de mesa |
            | capacity | INT | Capacidad máxima |
            | status | VARCHAR(20) | available / occupied / reserved |

            ## menu_categories
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | restaurant_id | UUID | FK |
            | name | VARCHAR(100) | Entradas, Platos Fuertes, etc. |
            | display_order | INT | Orden en el menú |

            ## menu_items
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | restaurant_id | UUID | FK |
            | category_id | UUID | FK → menu_categories |
            | name | VARCHAR(200) | |
            | description | TEXT | |
            | price | DECIMAL(10,2) | |
            | duration_minutes | INT | Tiempo estimado de preparación |
            | is_available | BIT | |

            ## orders (pedidos)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | restaurant_id | UUID | FK |
            | table_id | UUID | FK → tables |
            | server_id | UUID | FK → employees |
            | status | VARCHAR(20) | open / sent_to_kitchen / ready / closed |
            | total | DECIMAL(10,2) | |
            | opened_at | DATETIME | |
            | closed_at | DATETIME | |

            ## order_items
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | order_id | UUID | FK → orders |
            | menu_item_id | UUID | FK → menu_items |
            | quantity | INT | |
            | unit_price | DECIMAL(10,2) | Precio al momento del pedido |
            | notes | VARCHAR(500) | Modificaciones del cliente |
            | status | VARCHAR(20) | pending / cooking / ready / served |

            ## reservations (reservaciones)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | restaurant_id | UUID | FK |
            | client_name | VARCHAR(200) | |
            | client_phone | VARCHAR(20) | |
            | party_size | INT | |
            | reserved_at | DATETIME | |
            | status | VARCHAR(20) | pending / confirmed / seated / cancelled |

            ## Índices recomendados
            - `orders(restaurant_id, status)` — pedidos activos
            - `order_items(order_id, status)` — display de cocina
            - `reservations(restaurant_id, reserved_at)` — agenda de reservaciones
            """,

        ["hr_payroll"] = """
            ## organizations
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | name | VARCHAR(200) | |
            | tax_id | VARCHAR(50) | Cédula jurídica |
            | country | VARCHAR(50) | Default: 'Costa Rica' |
            | timezone | VARCHAR(50) | |
            | payroll_frequency | VARCHAR(20) | biweekly / monthly |

            ## departments
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | name | VARCHAR(100) | |
            | manager_id | UUID | FK → employees (nullable) |

            ## employees
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | department_id | UUID | FK → departments |
            | full_name | VARCHAR(200) | |
            | email | VARCHAR(200) | |
            | national_id | VARCHAR(20) | Cédula |
            | hire_date | DATE | |
            | position | VARCHAR(100) | Puesto |
            | base_salary | DECIMAL(12,2) | |
            | is_active | BIT | |

            ## shift_schedules (horarios asignados)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | employee_id | UUID | FK → employees |
            | day_of_week | INT | 0=Lunes … 6=Domingo |
            | start_time | TIME | |
            | end_time | TIME | |
            | effective_from | DATE | |
            | effective_to | DATE | nullable = indefinido |

            ## attendance_records (marcas de asistencia)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | employee_id | UUID | FK → employees |
            | date | DATE | |
            | check_in | DATETIME | |
            | check_out | DATETIME | nullable |
            | hours_worked | DECIMAL(5,2) | Calculado |
            | status | VARCHAR(20) | present / absent / late / holiday |

            ## payroll_periods (períodos de planilla)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | start_date | DATE | |
            | end_date | DATE | |
            | status | VARCHAR(20) | open / calculating / closed / paid |
            | closed_at | DATETIME | |

            ## payroll_entries (entradas de planilla por empleado)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | payroll_period_id | UUID | FK → payroll_periods |
            | employee_id | UUID | FK → employees |
            | regular_hours | DECIMAL(5,2) | |
            | overtime_hours | DECIMAL(5,2) | |
            | gross_salary | DECIMAL(12,2) | |
            | ccss_deduction | DECIMAL(12,2) | Caja del Seguro Social |
            | other_deductions | DECIMAL(12,2) | |
            | net_salary | DECIMAL(12,2) | |

            ## absences (ausencias y días libres)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | employee_id | UUID | FK → employees |
            | start_date | DATE | |
            | end_date | DATE | |
            | type | VARCHAR(20) | vacation / sick / personal / holiday |
            | status | VARCHAR(20) | pending / approved / rejected |
            | approved_by | UUID | FK → employees |
            | notes | TEXT | |

            ## Índices recomendados
            - `attendance_records(organization_id, date)` — asistencia por día
            - `attendance_records(employee_id, date)` — historial por empleado
            - `payroll_entries(payroll_period_id)` — planilla completa por período
            - `absences(employee_id, start_date)` — ausencias por empleado
            """,

        ["gaming"] = """
            ## organizations
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | name | VARCHAR(200) | |
            | country | VARCHAR(50) | |
            | license_number | VARCHAR(100) | Licencia de operación |

            ## branches (sucursales)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | name | VARCHAR(200) | |
            | address | VARCHAR(500) | |
            | is_active | BIT | |

            ## machines (máquinas)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | branch_id | UUID | FK → branches |
            | serial_number | VARCHAR(100) | |
            | model | VARCHAR(200) | |
            | manufacturer | VARCHAR(200) | |
            | status | VARCHAR(20) | active / warning / error / offline |
            | installed_at | DATE | |

            ## operators (operadores de campo)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | full_name | VARCHAR(200) | |
            | email | VARCHAR(200) | |
            | assigned_branch_id | UUID | FK → branches (nullable) |
            | role | VARCHAR(20) | operator / supervisor / admin |
            | is_active | BIT | |

            ## tasks (tareas de mantenimiento)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | machine_id | UUID | FK → machines |
            | assigned_to | UUID | FK → operators |
            | title | VARCHAR(300) | |
            | description | TEXT | |
            | priority | VARCHAR(20) | low / medium / high / critical |
            | status | VARCHAR(20) | pending / in_progress / completed / cancelled |
            | due_at | DATETIME | |
            | completed_at | DATETIME | |

            ## alerts (alertas)
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | organization_id | UUID | FK |
            | machine_id | UUID | FK → machines |
            | type | VARCHAR(50) | mechanical / electrical / connectivity / revenue |
            | severity | VARCHAR(20) | info / warning / critical |
            | message | TEXT | |
            | resolved_at | DATETIME | nullable |
            | created_at | DATETIME | |

            ## performance_snapshots
            | Campo | Tipo | Descripción |
            |-------|------|-------------|
            | id | UUID | PK |
            | machine_id | UUID | FK → machines |
            | recorded_at | DATETIME | |
            | revenue_period | DECIMAL(12,2) | Ingresos del período |
            | uptime_minutes | INT | |

            ## Índices recomendados
            - `machines(branch_id, status)` — estado por sucursal
            - `tasks(assigned_to, status)` — tareas por operador
            - `alerts(organization_id, resolved_at)` — alertas activas
            - `performance_snapshots(machine_id, recorded_at)` — series de tiempo
            """,
    };

    private static string GetDbSchema(string industry, ProductProfile p)
    {
        if (DbSchemas.TryGetValue(industry, out var schema)) return schema;

        // Generic schema based on profile entities
        var lines = new System.Text.StringBuilder();
        foreach (var entity in p.DbEntities)
        {
            var table = entity.ToLower().Replace(" ", "_");
            lines.AppendLine($"## {table}");
            lines.AppendLine("| Campo | Tipo | Descripción |");
            lines.AppendLine("|-------|------|-------------|");
            lines.AppendLine($"| id | UUID | PK |");
            lines.AppendLine($"| organization_id | UUID | FK → organizations |");
            lines.AppendLine($"| name | VARCHAR(200) | |");
            lines.AppendLine($"| is_active | BIT | |");
            lines.AppendLine($"| created_at | DATETIME | |");
            lines.AppendLine();
        }
        lines.AppendLine("## Índices recomendados");
        lines.AppendLine($"- `{p.DbEntities.First().ToLower()}s(organization_id, created_at)` — listado por organización");
        return lines.ToString();
    }

    // ── Public artifact generators ───────────────────────────────────────────

    public static string GenerateBrief(string name, ProductProfile p) => $"""
        # Product Brief: {name}

        ## Executive Summary
        **{name}** es un SaaS de {p.IndustryLabel} diseñado para {p.TargetUser.ToLower()}.
        Centraliza y automatiza las operaciones clave del negocio, eliminando herramientas
        fragmentadas y dando visibilidad en tiempo real a propietarios y equipos.

        ---

        ## El Problema
        {p.Problem}

        ### Síntomas concretos que sufre el usuario hoy
        - Datos dispersos en WhatsApp, Excel y notas físicas
        - Tiempo excesivo en tareas administrativas repetitivas
        - Sin métricas para tomar decisiones de negocio
        - Errores humanos costosos por procesos manuales
        - Escalar el negocio requiere contratar más personas, no mejores herramientas

        ---

        ## Usuario Objetivo
        **{p.TargetUser}**

        ### Perfil del tomador de decisión
        - **Dolor principal**: Opera sin visibilidad y con demasiado trabajo manual
        - **Motivación de compra**: Ahorrar tiempo, reducir errores, crecer sin caos
        - **Freno de compra**: Precio, curva de aprendizaje, resistencia al cambio del equipo
        - **Señal de éxito**: Puede gestionar el negocio desde su celular en 15 minutos al día

        ---

        ## Features Core Detectadas
        {string.Join("\n", p.CoreFeatures.Select((f, i) => $"### Feature {i + 1}: {f}\nPermite al usuario {f.ToLower()} con una interfaz intuitiva y sin fricción."))}

        ---

        ## Propuesta de Valor
        > **{name}** ayuda a {p.TargetUser.Split(" ").Take(4).Aggregate((a, b) => a + " " + b).ToLower()} a {p.CoreFeatures[0].ToLower()} y {p.CoreFeatures[1].ToLower()} sin depender de Excel ni WhatsApp, a diferencia de herramientas genéricas que no entienden el negocio de {p.IndustryLabel.ToLower()}.

        ---

        ## Modelo de Negocio Recomendado
        | Plan | Precio | Límites | Target |
        |------|--------|---------|--------|
        | Free | $0/mes | 1 usuario, funciones básicas | Evaluación |
        | Starter | $29/mes | 5 usuarios, todas las features | Negocio pequeño |
        | Pro | $79/mes | Usuarios ilimitados + API | Cadenas y franquicias |

        ---

        ## Stack Tecnológico
        - **Backend**: .NET 9 Web API (Clean Architecture)
        - **Base de datos**: SQL Server / Azure SQL
        - **Frontend**: Next.js 15 + TypeScript + Tailwind
        - **Auth**: ASP.NET Core Identity + JWT
        - **Pagos**: Stripe Checkout + Webhooks
        - **Email**: Resend / SendGrid
        - **Deploy**: Azure App Service + Azure SQL

        ---

        ## Métricas de Éxito (MVP)
        - 10 clientes pagos en 60 días
        - NPS > 40 en primeros usuarios
        - < 5 minutos para onboarding completo
        - 0 errores críticos en producción primer mes
        """;

    public static string GenerateArchitecture(string name, ProductProfile p) => $"""
        # Arquitectura del Sistema: {name}

        ## Stack Tecnológico
        | Capa | Tecnología | Razón |
        |------|-----------|-------|
        | Backend | .NET 9 Web API | Clean Architecture, rendimiento, ecosistema empresarial |
        | Base de datos | SQL Server / SQLite (dev) | Transacciones ACID, Azure SQL managed |
        | ORM | EF Core 9 | Migraciones automáticas, LINQ type-safe |
        | Frontend | Next.js 15 + TypeScript | SSR/SSG, App Router, ecosistema React |
        | Auth | ASP.NET Core Identity + JWT | Roles, claims, refresh tokens, multi-tenant |
        | Deploy | Azure App Service | PaaS managed, auto-scaling, CI/CD nativo |

        ---

        ## Patrón Arquitectónico
        **{p.ArchitecturePattern}**

        ### Decisiones de diseño clave
        1. **Multi-tenancy por OrganizationId**: Todas las tablas de negocio incluyen `organization_id`. Sin Row Level Security compleja — el filtro es responsabilidad del servicio.
        2. **Clean Architecture**: Domain → Infrastructure → API. Sin MediatR en V1 para mantener simplicidad.
        3. **JWT stateless**: Tokens de 15 minutos + refresh de 7 días. Sin sesiones server-side.
        4. **Soft deletes**: Entidades críticas usan `is_active` en lugar de DELETE físico.

        ---

        ## Módulos del Sistema
        {string.Join("\n\n", p.CoreFeatures.Select((f, i) => $"### Módulo {i + 1}: {f}\n**Responsabilidad**: {f}.\n**Entidades involucradas**: {(i < p.DbEntities.Length ? p.DbEntities[i] : p.DbEntities.Last())}.\n**Endpoints principales**: GET (listado paginado), GET/:id, POST, PUT/:id, DELETE/:id."))}

        ---

        ## Relaciones Clave entre Entidades
        ```
        {p.DbEntities[0]}
        {string.Join("\n", p.DbEntities.Skip(1).Take(4).Select(e => $"    └── {e} (organization_id FK)"))}
        ```

        ---

        ## Estrategia de Auth & Autorización
        - **Roles**: `owner`, `manager`, `employee` (configurable por organización)
        - **Claims en JWT**: `organization_id`, `user_id`, `role`
        - **Guards**: Middleware de tenant extrae org del token y filtra todas las queries

        ---

        ## API Design
        - RESTful con versionado `/api/v1/...`
        - Paginación cursor-based para listas grandes
        - DTOs de respuesta — nunca exponer entidades directamente
        - FluentValidation en todos los inputs
        - Global exception handler con ProblemDetails (RFC 7807)

        ---

        ## Infraestructura
        | Ambiente | Stack |
        |----------|-------|
        | Local dev | SQLite + HTTP |
        | Staging | Azure App Service (B1) + Azure SQL Basic |
        | Producción | Azure App Service (P1v3) + Azure SQL Standard |

        ---

        ## Riesgos Técnicos y Mitigaciones
        | Riesgo | Severidad | Mitigación |
        |--------|-----------|------------|
        | {p.TechnicalRisk.Split('.').First()} | Alta | Diseñar desde el inicio, no como afterthought |
        | Escalabilidad de DB | Media | Índices correctos, paginación, query profiling |
        | Seguridad multi-tenant | Alta | Tests de penetración básicos antes de prod |
        | Vendor lock-in Azure | Baja | Abstracciones sobre servicios cloud |
        """;

    public static string GenerateDbSchema(string name, ProductProfile p)
    {
        var schema = GetDbSchema(p.Industry, p);
        return $"""
            # Esquema de Base de Datos: {name}

            ## Patrón de Multi-tenancy
            Todas las tablas de negocio incluyen `organization_id` (UUID, NOT NULL, FK → organizations).
            Esto garantiza aislamiento por organización sin Row Level Security compleja.
            El filtro se aplica en la capa de repositorio para todas las queries.

            ---

            ## Entidades Principales

            {schema}

            ---

            ## Notas de Implementación

            ### Soft deletes
            Las entidades con `is_active BIT` deben usar filtros globales en EF Core:
            ```csharp
            modelBuilder.Entity<Employee>().HasQueryFilter(e => e.IsActive);
            ```

            ### Convenciones de naming
            - Tablas: snake_case plural (employees, shift_schedules)
            - PKs: siempre UUID generado en aplicación (no identity)
            - FKs: `{"{table_name}"}_id`
            - Timestamps: `created_at`, `updated_at` en toda entidad mutable

            ### Migraciones
            Usar EF Core Migrations. Nunca editar migraciones ya aplicadas en staging/prod.
            Cada cambio de schema = nueva migración con nombre descriptivo.

            ### Índices de performance
            Ver sección de índices recomendados por tabla.
            Revisar execution plans en staging antes de deploy a producción.
            """;
    }

    public static string GenerateRoadmap(string name, ProductProfile p)
    {
        var sprints = p.SprintPlan;
        return $"""
            # MVP Roadmap: {name}

            ## Resumen ejecutivo
            **{name}** se construirá en **{sprints.Length} sprints** de 2 semanas cada uno.
            **Tiempo estimado al MVP**: {sprints.Length * 2} semanas.
            **Deploy a staging**: Al finalizar Sprint {sprints.Length}.
            **Deploy a producción**: 1 semana después de validación en staging.

            ---

            {string.Join("\n\n", sprints.Select((s, i) =>
                {
                    var parts = s.Split(":");
                    var sprintName = parts[0].Trim();
                    var features = parts.Length > 1 ? parts[1].Trim() : s;
                    var weeks = i == 0 ? "Semanas 1-2" : i == 1 ? "Semanas 3-4" : i == 2 ? "Semanas 5-6" : "Semanas 7-8";
                    return $"""
                        ## {sprintName} — {weeks}
                        **Deliverables**: {features}

                        **Criterios de aceptación**:
                        - [ ] Endpoints funcionando con tests unitarios
                        - [ ] UI conectada al backend
                        - [ ] Datos de prueba cargados
                        - [ ] Build + deploy a entorno local sin errores
                        - [ ] Code review completado

                        **Definición de "done"**: Feature accesible en localhost con datos reales y sin errores en consola.
                        """;
                }))}

            ---

            ## Features post-MVP (backlog)
            - Analytics avanzado y exportación de reportes
            - App móvil (React Native)
            - Integración con servicios externos
            - Plan Enterprise y funciones avanzadas
            - API pública para integraciones de terceros

            ---

            ## Riesgos del roadmap
            | Riesgo | Probabilidad | Plan de contingencia |
            |--------|-------------|---------------------|
            | Scope creep en Sprint 1-2 | Alta | Congelar features antes de cada sprint |
            | {p.TechnicalRisk.Split('.').First()} | Media | Spikes técnicos en Sprint 1 |
            | Baja adopción inicial | Media | Beta cerrada con 5 usuarios antes de lanzar |
            """;
    }

    public static string GenerateSprintPlan(string name, ProductProfile p, int sprintNumber)
    {
        var sprintIdx  = Math.Clamp(sprintNumber - 1, 0, p.SprintPlan.Length - 1);
        var sprintDesc = p.SprintPlan[sprintIdx];
        var parts      = sprintDesc.Split(":");
        var sprintName = parts[0].Trim();
        var features   = parts.Length > 1 ? parts[1].Trim() : sprintDesc;

        return $"""
            # Sprint {sprintNumber} Plan: {name}

            ## Nombre del Sprint
            **{sprintName}** — {features}

            ## Duración
            2 semanas (10 días hábiles)

            ---

            ## User Stories

            {string.Join("\n\n", p.CoreFeatures.Take(3).Select((f, i) => $"""
            ### US-{sprintNumber}{i + 1:00}: {f}
            **Como** usuario autenticado de {name},
            **Quiero** {f.ToLower()},
            **Para** gestionar mi operación sin herramientas externas.

            **Criterios de aceptación**:
            - [ ] Puedo acceder desde desktop y mobile
            - [ ] Los datos se guardan inmediatamente
            - [ ] Los errores se muestran de forma clara
            - [ ] La operación tarda menos de 2 segundos
            """))}

            ---

            ## Tareas Técnicas

            ### Backend
            - [ ] Definir y aplicar migración de base de datos
            - [ ] Implementar DTOs de request y response
            - [ ] Crear endpoints CRUD con validación FluentValidation
            - [ ] Tests unitarios del dominio (> 80% coverage)
            - [ ] Tests de integración de los endpoints principales

            ### Frontend
            - [ ] Crear página con listado + paginación
            - [ ] Formulario de creación/edición con validación client-side
            - [ ] Manejo de estados: loading, error, empty state
            - [ ] Responsivo en mobile (375px) y desktop (1440px)
            - [ ] Conectar con API real (no mocks)

            ### Quality Gate
            - [ ] `dotnet build` → 0 errores, 0 warnings
            - [ ] `dotnet test` → todos pasan
            - [ ] `tsc --noEmit` → 0 errores
            - [ ] Code review por segundo par de ojos
            - [ ] Deploy a entorno staging

            ---

            ## Estimación
            | Área | Días estimados |
            |------|---------------|
            | Backend (endpoints + tests) | 4 días |
            | Frontend (UI + integración) | 4 días |
            | QA + ajustes + deploy | 2 días |
            | **Total** | **10 días** |

            ---

            ## Notas del Runtime
            Sprint generado automáticamente basado en el perfil de {p.IndustryLabel}.
            Los detalles específicos de implementación se refinarán con el equipo antes de iniciar.
            """;
    }
}
