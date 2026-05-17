# Builder Platform

Runtime de generación de SaaS — orquestador que convierte una idea en un proyecto Next.js + .NET funcional, con preview en vivo, quality gates y autofix automático.

## Stack

| Capa | Tecnología |
|------|-----------|
| Backend | .NET 9 Web API + EF Core + SQLite (dev) |
| Frontend | Next.js 16 + TypeScript + Tailwind v4 |
| Runtime | BackgroundService + Channel (no MediatR) |
| DB | SQLite en desarrollo → SQL Server en producción |

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
└── generated/                  # [IGNORED] SaaS projects generados en runtime
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

# Copiar config local
cp .env.local.example .env.local

# Instalar dependencias
npm install

# Correr en dev
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

## Migraciones EF Core

Las migraciones se aplican automáticamente al iniciar el backend en desarrollo.
Para crear una nueva migración:

```bash
cd backend
dotnet ef migrations add <NombreMigracion> --project src/BuilderPlatform.Infrastructure --startup-project src/BuilderPlatform.API
```

## Notas de configuración

- `appsettings.Development.json` — **NO versionado** (contiene `OutputPath` local). Copiar desde `.example`.
- `.env.local` — **NO versionado** (contiene URL de API). Copiar desde `.example`.
- `generated/` — **NO versionado** (output de runtime, puede ser GBs). Configurar `Scaffold:OutputPath` fuera del repo si se prefiere.
- `*.db` — **NO versionado** (SQLite local).
