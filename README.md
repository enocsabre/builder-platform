# Builder Platform

Runtime de generación de SaaS — orquestador que convierte una idea en un proyecto Next.js + .NET funcional, con módulos operacionales domain-aware, preview en vivo, quality gates y deploy automático a Vercel.

**URL pública de demo**: https://frontend-two-beta-79.vercel.app

## Qué hace

1. El usuario describe un SaaS en lenguaje natural ("restaurante con mesas, pedidos, inventario")
2. El runtime detecta la industria, genera la arquitectura y pide aprobación
3. El scaffold genera ~40 archivos reales: backend .NET + frontend Next.js + design system operacional
4. Cada módulo generado tiene columnas, datos y estados específicos del dominio — no CRUD genérico
5. El usuario puede pedir features adicionales en chat; el runtime las genera como bundles de 5–6 archivos
6. Quality gates (build + typecheck + registry) validan el proyecto; autofix repara failures automáticamente
7. El preview corre el frontend generado en vivo (puerto 3100–3200); cleanup automático de zombies al reiniciar
8. El proyecto generado se puede abrir directamente en VS Code desde el workspace
9. **Simulation Engine**: 4 escenarios operacionales (hora pico, cocina congestionada, bajo inventario, operación normal) generan datos realistas en tiempo real — el producto se siente vivo durante demos sin ingresar datos manualmente
10. Deploy a Vercel con un comando

## Stack

| Capa | Tecnología |
|------|-----------|
| Backend | .NET 9 Web API + EF Core + SQLite (dev) |
| Frontend | Next.js 16 + TypeScript + Tailwind v4 |
| Runtime | BackgroundService + Channel (no MediatR) |
| Design system generado | Operational Dark (CSS vars semánticos) |
| DB | SQLite en desarrollo → SQL Server en producción |
| Deploy | Vercel (via CLI — npx vercel --prod) |

## Estructura del repo

```
builder-platform/
├── backend/                    # .NET 9 Web API
│   └── src/
│       ├── BuilderPlatform.API/           # Controllers, DTOs, Program.cs
│       ├── BuilderPlatform.Domain/        # Entities, enums
│       └── BuilderPlatform.Infrastructure/# DbContext, Services, Migrations
├── frontend/                   # Next.js 16 workspace UI
│   ├── app/                    # App Router pages
│   ├── components/             # UI components
│   └── lib/                    # types.ts, utils
└── generated/                  # [IGNORADO] SaaS projects generados en runtime
```

## Setup local

### Prerequisitos
- .NET 9 SDK
- Node.js 20+
- Git

### Backend

```bash
cd backend

# Copiar config local
cp src/BuilderPlatform.API/appsettings.Development.json.example \
   src/BuilderPlatform.API/appsettings.Development.json

# Editar OutputPath en appsettings.Development.json con tu ruta absoluta a /generated

# Correr (migra automáticamente al iniciar)
dotnet run --project src/BuilderPlatform.API
```

Backend disponible en: **http://localhost:5238**

### Frontend

```bash
cd frontend

cp .env.local.example .env.local

npm install
npm run dev
```

Frontend disponible en: **http://localhost:3002**

## Puertos

| Servicio | Puerto |
|---------|--------|
| Backend API | 5238 |
| Frontend UI | 3002 |
| Generated preview (primero disponible) | 3100–3200 |

## Comandos de build / calidad

```bash
# Backend — build limpio
dotnet build --nologo -v quiet

# Backend — tests
dotnet test --nologo

# Frontend — TypeScript check
npx tsc --noEmit

# Frontend — build de producción
npm run build
```

## Industrias con módulos domain-aware

El scaffold genera módulos operacionales para 9 industrias:

| Industria | Key | Módulos ejemplo |
|-----------|-----|----------------|
| Restaurantes | `restaurant` | Mesas (ocupación), Pedidos (órdenes/estado), Cocina KDS, Menú, Inventario |
| Veterinaria | `veterinary` | Pacientes, Citas, Historial médico, Inventario |
| RRHH / Planilla | `hr_payroll` | Empleados, Asistencia, Nómina, Vacaciones |
| Gaming | `gaming` | Jugadores, Torneos, Rankings, Métricas |
| Real Estate | `real_estate` | Propiedades, Clientes, Visitas, Contratos |
| Salud | `healthcare` | Pacientes, Citas, Expedientes, Facturación |
| Educación | `education` | Estudiantes, Cursos, Calificaciones, Asistencia |
| E-commerce | `ecommerce` | Productos, Pedidos, Clientes, Inventario |
| Logística | `logistics` | Envíos, Rutas, Flota, Clientes |

Cada módulo tiene: columnas operacionales del dominio, datos coherentes entre sí, badge de estado semántico, KPI bar contextual, acción primaria específica.

## Cross-module coherence (ejemplo restaurante)

Los datos generados son consistentes entre módulos:
- **Orden #1247 · Mesa 4 · Casado Típico x2** → aparece en Pedidos ("Preparando") y en Cocina KDS ("En preparación")
- **Arroz** → aparece en Inventario (Bajo stock / danger) y en dashboard activity ("Inventario: Arroz bajo stock")
- **Mesa 4** → aparece en Mesas (Ocupada · Sofía M.), en Pedidos (fuente de #1247), en Cocina (#1247 en preparación)

## Regla de demo-readiness

> Un SaaS generado no está demo-ready si sus módulos siguen siendo CRUD genéricos.

Un módulo genérico tiene "Demo Item 1 / Nombre / Estado / Fecha". Un módulo demo-ready tiene columnas y datos que permiten entender el negocio en 5 segundos sin leer documentación.

## Migraciones EF Core

Las migraciones se aplican automáticamente al iniciar el backend en desarrollo.

```bash
cd backend
dotnet ef migrations add <NombreMigracion> \
  --project src/BuilderPlatform.Infrastructure \
  --startup-project src/BuilderPlatform.API
```

### Historial de migraciones (en orden, nunca editar existentes)

| Migración | Qué agrega |
|-----------|-----------|
| `Sprint1_RuntimeEngine` | Products, ChatMessages, ActivityEvents, Approvals, ProductMemory |
| `Sprint2_ArtifactSystem` | Artifacts + ArtifactId FK |
| `Sprint3_ScaffoldEngine` | ScaffoldEntries + ProjectPath/ScaffoldStatus |
| `Sprint4_FeatureExecution` | ScaffoldChanges |
| `Sprint5_ProjectAwareness` | ProductModules |
| `Sprint8_PreviewRunner` | Columnas preview en Products |
| `Sprint9_FileRevisions` | FileRevisions + cascade + index |
| `Sprint10_ValidationRuns` | RuntimeHealth + ValidationRuns |
| `Sprint10b_Fix` | Empty (sync snapshot) |
| `Sprint11_Deploy` | DeployRuns + 7 columnas deploy en Products |

## Notas de configuración

- `appsettings.Development.json` — **NO versionado**. Copiar desde `.example`. Contiene `OutputPath`, `Deploy:VercelToken`, `Deploy:VercelScope`.
- `.env.local` — **NO versionado**. Contiene URL de API.
- `generated/` — **NO versionado**. Output de runtime, puede ser GBs.
- `*.db` — **NO versionado**. SQLite local.

## Sprint history

Ver [docs/SPRINT_HISTORY.md](docs/SPRINT_HISTORY.md) para el historial completo de sprints y decisiones técnicas.
