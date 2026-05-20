# Builder Platform — Sprint History

Historial técnico de decisiones, features y bugs por sprint. Leer antes de tocar código existente.

---

## Sprint 1 — Runtime Orchestration MVP

**Qué se construyó**: Pipeline completo de creación de producto — Draft → Discovering → Architecting → Planning → Building. BackgroundService + Channel para non-blocking orchestration. IntentEngine con keyword matching (8 intents). ContentGenerator con 10 industrias (brief, architecture, roadmap, mensajes). ProductMemory como key-value store. Approval checkpoints con pausa en `waiting_approval`.

**Decisiones clave**:
- Sin MediatR — BackgroundService + Channel es suficiente y más predecible
- SQLite en dev, EF Core migrations automáticas al startup
- ContentGenerator es clase estática — sin DI, sin estado, pura generación de texto

**Migración**: `Sprint1_RuntimeEngine`

---

## Sprint 2 — Artifact System

**Qué se construyó**: Entidad `Artifact` (Type, Content, Version, Status). ArtifactsController (GET list, GET single, POST approve). Frontend: ArtifactViewer modal + MarkdownRenderer custom (sin libs externas). Los artifacts se vinculan a ActivityEvents y Approvals via ArtifactId FK.

**Migración**: `Sprint2_ArtifactSystem`

---

## Sprint 3 — Scaffold Engine

**Qué se construyó**: ScaffoldEngine genera ~40 archivos reales por producto: sln + 3 csproj, N entidades .NET, AppDbContext, N controllers, Next.js app completa (layout, dashboard, login, N feature pages, Sidebar, Header, lib/). ScaffoldEntry en DB por archivo. Safety: carpeta existente → append timestamp suffix.

**Decisiones técnicas clave**:
- Template strategy: `$"""` para C#/XML, `"""` + `.Replace()` para TSX — evita conflictos con JSX `{{ }}`
- Operational Dark design system embebido en globals.css generado (CSS vars semánticos)
- `ScaffoldEngine` registrado como singleton, recibe `IConfiguration` para `Scaffold:OutputPath`

**Migración**: `Sprint3_ScaffoldEngine`

---

## Sprint 4 — Feature Execution Engine

**Qué se construyó**: IntentEngine keyword `feature_request`. HandleFeatureExecution genera 3 archivos delta (entity .cs, controller .cs, frontend page.tsx). ScaffoldChange en DB (ChangeType: created/skipped). Idempotencia: segundo "agrega X" → todos SKIPPED, archivos intactos. Frontend: tab "Cambios" con ChangesPanel grouping por módulo.

**Bug crítico corregido**: GetById con 7+ Include() collections causaba Cartesian join timeout → fixed con `.AsSplitQuery()`

**Migración**: `Sprint4_FeatureExecution`

---

## Sprint 5 — Project Awareness Engine

**Qué se construyó**: ProductModule entity (ModuleName, EntityName, RoutePath, ControllerName, Layer, Source: scaffold|delta). ProjectAwarenessEngine: `ScanAndRegisterAsync` (lee .cs entities + controllers + frontend routes), `RegisterDeltaModuleAsync` (post-feature), `ModuleExistsAsync`. Registry files: `frontend/registry/modules.json`, `nav-items.json`, `dashboard.json`.

**Bug**: Anti-double-plural en ResolveRoute — si entity ya termina en 's', no agregar otro 's' al fallback.

**Migración**: `Sprint5_ProjectAwareness`

---

## Sprint 6 — Runtime-Assisted Product Evolution

**Qué se construyó**: DashboardRequest intent. HandleDashboardUpdate genera widget TSX (`components/widgets/{pascal}Widget.tsx`). CheckDashboardAsync + AddWidgetToRegistryAsync (idempotente por nombre). CheckAndUpdateNavAsync (idempotente por href). NormalizeForPath strips Spanish accents (safe filenames).

**Decisión**: `internal static NormalizeForPath` en ProjectAwarenessEngine — accesible desde ScaffoldEngine sin duplicar.

---

## Sprint 7 — Runtime Integration & Generated Execution

**Qué se construyó**: HandleFeatureExecution reescrito como coordinador de 6 pasos: entity + controller + page → nav → widget → registry → artifact → mensaje bundle. ChangesPanel agrupa por módulo + layer (Backend/Frontend/Navegación/Dashboard/Registry). Badge "bundle" cuando módulo tiene >1 layer activo. Idempotencia full-bundle.

**E2E validado**: scaffold 43 archivos, bundle Inventario (6 CREATED), repetir idempotente (6 SKIPPED), dashboard standalone Reportes (1 CREATED). Fecha: 2026-05-16.

---

## Sprint 8 — Generated App Preview MVP

**Qué se construyó**: PreviewRunner singleton (`ConcurrentDictionary<Guid, RunningPreview>`). Port allocation TcpListener (rango 3100–3200). Ready detection via stdout watching. Stop via `taskkill /F /T /PID`. PreviewPanel.tsx con 4 estados visuales. Tab "Preview" con badge verde.

**Bugs críticos**:
1. UTF-8 BOM en archivos generados → Node.js rechaza JSON. Fix: `new System.Text.UTF8Encoding(false)` en todos los `File.WriteAllTextAsync`
2. Turbopack incompatible con Tailwind v4 PostCSS → usar `next dev` (webpack)
3. `postcss.config.mjs` faltante → ScaffoldEngine lo escribe
4. Node.js 25 `localStorage` global broken → `instrumentation.ts` parchea SSR

**E2E validado**: preview start → running (puerto 3100) → HTTP 200 dashboard → stop. Fecha: 2026-05-16.

**Migración**: `Sprint8_PreviewRunner`

---

## Sprint 9 — Runtime File Awareness & Controlled Editing

**Qué se construyó**: FileRevision entity (before/after content, 8000 chars cap). RuntimePatchEngine con managed zone registry (7 files, EDIT/LOCK). PatchOperation enum: NavPushDashboardFirst, DashboardPremiumUpgrade, DashboardAddQuickStats. HandleUiEvolution: classify → apply → auto-restart preview. FilesPanel.tsx con side-by-side diff modal.

**Managed zones**: nav-items.json (EDIT), dashboard.json (EDIT), modules.json (LOCK), globals.css (EDIT), dashboard/page.tsx (EDIT), layout.tsx (LOCK), next.config.ts (LOCK).

**E2E validado**: nav_reorder, dashboard_premium, dashboard_quick_stats. Idempotencia y self-healing. Fecha: 2026-05-16.

**Migración**: `Sprint9_FileRevisions`

---

## Sprint 10 — Runtime Quality & Autofix MVP

**Qué se construyó**: RuntimeValidationEngine — 7 gates en 3 categorías (Registry/Runtime/Build). AutofixEngine — 2 estrategias por gate (restore from revision o restart). HandleValidation — max 2 rondas autofix. runtimeHealth: healthy/degraded/broken. QualityPanel.tsx con gate results expandibles. Startup cleanup para stale ValidationRuns.

**Bugs**:
1. `fixed` es keyword reservada en C# → renombrar a `wasFixed`
2. `StringComparison.OrdinalIgnoreCase` no soportado en SQLite LINQ → `.ToLower()` comparison
3. AutofixEngine timeout 35s muy corto → 65s

**E2E validado**: Registry autofix, build gates, preview autofix, chat-triggered validation. Fecha: 2026-05-17.

**Migraciones**: `Sprint10_ValidationRuns` + `Sprint10b_Fix`

---

## Sprint 11 — Deploy Pipeline MVP

**Qué se construyó**: DeployEngine con Vercel adapter (npx vercel --prod). URL parser con prioridad `▲ Aliased` (sin Deployment Protection). GetGitInfoAsync. HandleDeploy con safety checks. DeployRun entity. DeployPanel.tsx. Tab "Deploy" con Rocket icon.

**Config requerida en appsettings.Development.json**:
- `Deploy:Provider = vercel`
- `Deploy:VercelToken = ""` (CLI usa device auth — vacío si CLI autenticado)
- `Deploy:VercelScope = enocsabre-4287` (personal scope slug — `vercel teams switch` previo al deploy)

**Bugs**:
1. CVE-2025-66478: Next.js 15.3.0 rechazado por Vercel → upgrade a 15.3.9
2. `--scope` falla en cuentas personales → `vercel teams switch {scope}` en su lugar
3. Deployment Protection en team URLs → preferir URL `▲ Aliased`

**Primer deploy real**: https://frontend-two-beta-79.vercel.app (2026-05-18). Gates: 6 PASS, 1 SKIP (preview_running — runtime-local).

**Migración**: `Sprint11_Deploy`

---

## Sprint 12 — Product Quality Generation MVP

**Qué se construyó**: `DomainContext` record + `GetDomainContext(industry)` en ContentGenerator — 10 industrias con KPIs y activity rows reales del dominio. Dashboard generado con KPI cards (valores domain-aware) + tabla "Actividad reciente" (5 rows, badges semánticos). Entity labels pipeline: ScaffoldEngine escribe `entity-labels.json` → ProjectAwarenessEngine lo lee → nav-items.json muestra "Mesas" en vez de "Tables".

**Bug corregido**: Sidebar mostraba nombres técnicos en inglés (Orders, Tables). Fix doble: ScaffoldEngine escribe labels, ProjectAwarenessEngine los aplica.

**E2E validado** (producto TestRestauranteQ12):
- Dashboard: ₡2,340,500 · 23 órdenes · 8/12 mesas · 47 ítems
- Actividad: Mesa 4 Casado Típico, Orden #1247, alerta inventario, reservación
- Sidebar: Dashboard, Mesas, Menú, Pedidos, Inventario, Cocina (KDS), Reservaciones
- Feature pages aún eran genéricas ("Demo Item 1/2/3") — resuelto en Sprint 13

URL pública: https://frontend-two-beta-79.vercel.app (2026-05-18)

---

## Sprint 13 — Domain-Aware Module Generation

**Objetivo**: Pasar de módulos CRUD genéricos a módulos operacionales que se sienten reales.

**Qué se construyó**:

### Records nuevos en ContentGenerator
```csharp
public record ModuleRow(string[] Cells, string StatusColor);
public record ModuleTemplate(
    string      Title,
    string      ActionLabel,
    string      KpiBar,
    string[]    Columns,
    int         StatusColumnIndex,  // -1 = sin badge de estado
    ModuleRow[] Rows
);
```

### GetModuleTemplate(feature, route, industry)
Retorna la plantilla operacional para la combinación de industria + módulo. Helpers:
- `Norm(s)`: normaliza acentos (áéíóúñü → aeiounu) — crítico porque `ToRoute()` elimina acentos
- `MT(f, r, keys)`: keyword matching contra feature normalizado + route directo

### FeaturePage() en ScaffoldEngine
Reescrito para llamar `GetModuleTemplate`. Si retorna `null` → cae a `GenericFeaturePage` (fallback). Template TSX con:
- `statusCol: __STATUS_COL__ as number` — previene TypeScript literal narrowing
- `rows: [...] as { cells: string[]; sc: string }[]` — previene string literal over-narrowing

### Restaurant — módulos generados (E2E validado)

| Ruta | Título | Columnas | statusCol |
|------|--------|----------|-----------|
| `/mesas` | Mesas | Mesa · Capacidad · Estado · Ocupada desde · Mesero | 2 |
| `/pedidos-comandas` | Pedidos | Orden · Mesa · Ítems · Total · Hora · Estado | 5 |
| `/men` | Menú | Nombre · Categoría · Precio · Tiempo prep. · Disponible | 4 |
| `/display` | Cocina (KDS) | Orden · Mesa · Ítems · Espera · Prioridad · Estado | 5 |
| `/inventario-compras` | Inventario | Producto · Unidad · Stock · Mínimo · Último movimiento · Estado | 5 |

### Cross-module coherence confirmada

| Objeto | Dónde aparece |
|--------|--------------|
| Orden #1247 · Mesa 4 · Casado Típico x2 | Pedidos ("Preparando") + Cocina KDS ("En preparación") |
| Arroz bajo stock | Inventario (danger) + dashboard activity ("Inventario: Arroz bajo stock") |
| Mesa 4 · Sofía M. | Mesas (Ocupada 1h 12m) + Pedidos (Mesa 4 → #1247) |
| KPI "23 órdenes" | Dashboard KPI card + Pedidos KPI bar |

**Bug corregido durante validación**: Backend corriendo con DLL vieja al scaffoldear Sprint13Restaurant. Fix: kill PID → rebuild → restart → producto nuevo (Sprint13V2).

**E2E validado** (producto Sprint13V2, 2026-05-19):
- 0 errores TypeScript
- 0 errores build
- Todas las rutas domain-aware (no más "Demo Item 1/2/3")
- Cross-module coherence confirmada
- Deploy: https://frontend-two-beta-79.vercel.app

---

---

## Sprint 14 — Feature Name Intelligence + Preview Stability

**Objetivo**: Extraer nombres de feature limpios del lenguaje natural + hacer el preview robusto post-ready.

### Pilar 1 — Feature Name Intelligence

**Problema**: "agrega gestión de proveedores al sistema" → generaba "Gestión De Proveedores Al Sistema".

**`ExtractFeatureName` en RuntimeOrchestrator.cs** — reescrito con 5 pasos:
1. Strip verb prefixes (agrega, agregá, necesito, implementá, etc.)
2. Strip trailing context noise ("al sistema", "a la app", "por favor", etc.) — loop hasta no más matches
3. Strip leading admin nouns — con `Norm(s)` normalizado para matching sin depender de acentos del input
4. Si 4+ palabras restantes, tomar primer noun ≥4 chars que no sea stopword
5. Title-case del resultado

**`ToDeltaRoute()` fix**: normalizaba acentos DESPUÉS del regex → "Facturación" → `facturacin`. Fix: normalizar primero → "facturacion" ✓.

**Registry coherence**: el nombre limpio se propaga a Activity, mensaje de runtime, route, sidebar label, widget name, entity/controller.

**E2E validado** (producto NamingTest):
- "agrega gestion de proveedores al sistema" → **Proveedores** / `/proveedores` ✓
- "necesito manejo de facturacion" → **Facturacion** / `/facturacion` ✓
- "agrega modulo de clientes" → **Clientes** / `/clientes` ✓

### Pilar 2 — PreviewRunner Stability

**3 cambios en PreviewRunner.cs**:

1. **Port probe paralelo** (`ProbePortAsync`): sondeo del puerto cada 2s (con 4s de head start) en paralelo al stdout. Lo que dispare primero marca "running".

2. **Watchdog por proceso** (`WatchProcessAsync`): monitorea el proceso post-ready. Cuando muere: limpia `_running`, vuelve a "starting", auto-restart una vez.

3. **Auto-restart único**: `isRestart: bool` param en `StartInternalAsync`. Si el restart también falla, fija estado a "error" sin más intentos.

**E2E validado**: preview start → running → HTTP 200 en todas las rutas → watchdog activities visibles.

---

## Sprint 15 — Streaming Runtime Presence

**Objetivo**: El frontend refleja en tiempo real qué hace el runtime — sin polling agresivo.

### Arquitectura: SSE + In-memory Event Bus

- `RuntimeEventBus.cs` — singleton pub/sub (`ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Func<string,string,Task>>>`)
- `frontend/lib/stream.ts` — hook `useProductStream(productId, { onPing, onStep })`

**Endpoint**: `GET /api/products/{id}/events` — `text/event-stream`, heartbeat cada 20s, bounded channel 64 (DropOldest), cleanup en desconexión.

**Tipos de eventos**:
- `heartbeat: connected` — inmediato al conectar
- `heartbeat: ping` — cada 20s (keep-alive)
- `ping:` — señal "algo cambió, recargá"
- `step: {título}` — paso actual del runtime para chat bubble progression

**Publish points**:
- `RuntimeOrchestrator`: `PingAsync()` después de cada `SaveChangesAsync()`; `StepAsync()` en `HandleScaffold` y `HandleFeatureExecution`
- `PreviewRunner`: `PingAsync()` en `UpdateStateAsync()`; `StepAsync` para npm install y preview start

**Frontend**: polling reducido a 15s safety net (3s para preview-starting y deploy activo). Chat "thinking" indicator muestra el step recibido en tiempo real.

**E2E validado** (2026-05-19): SSE heartbeat inmediato ✓ · feature execution → steps en tiempo real ✓ · dotnet build 0 errores ✓ · tsc 0 errores ✓ · next build limpio ✓

---

## Sprint 16 — Preview Zombie Cleanup & Runtime Hardening

**Objetivo**: Eliminar procesos Node.js zombie de sesiones anteriores al reiniciar el backend.

### PID Registry

Archivo JSON en `Path.GetTempPath()/bp-preview-pids.json` — persiste entre reinicios del backend.

- `PidEntry` record: `ProductId`, `Pid`, `Port`, `FrontendPath`, `StartedAt`
- `SemaphoreSlim(1,1)` para acceso thread-safe al archivo
- `RegisterPidAsync` / `UnregisterPidAsync` / `ReadRegistryAsync` — helpers privados

### StartupCleanupAsync

Llamado desde `RuntimeOrchestrator.StartAsync` antes de `base.StartAsync(ct)`:
1. Lee el PID registry
2. Para cada entrada: verifica que el proceso existe y que su nombre ∈ {node, cmd, npm}
3. Si seguro: `Kill(true)` (kill tree)
4. Limpia el registry
5. Resetea `previewStatus` en DB a "stopped" para todos los productos en estado running/starting

**Regla de seguridad**: solo mata PIDs registrados por nosotros Y verificados por nombre de proceso — defensa en profundidad contra PID reuse.

### FindFreePort fix

Reemplazó `new TcpListener(IPAddress.Loopback, port)` por `IPGlobalProperties.GetActiveTcpListeners()`:
- Razón: en Windows, bind a `127.0.0.1:X` tiene éxito aunque `0.0.0.0:X` esté tomado (SO_REUSEADDR) → falsos negativos
- `GetActiveTcpListeners()` consulta el OS directamente

### Early-exit fix

`proc.HasExited` en `StartInternalAsync` ahora llama `UnregisterPidAsync` antes de retornar — evita entradas huérfanas en el registry.

**E2E validado** (2026-05-19):
- Zombie cleanup: backend restart → logs confirman kill de PIDs registrados ✓
- DB reset: productos stale → stopped ✓
- Safety check: PID de proceso del sistema (AcPowerNotification) → SKIPPED (nombre no en allowlist) ✓
- Nueva sesión: preview start → running → HTTP 200 ✓

---

## Sprint 17 — VS Code Runtime Integration

**Objetivo**: Abrir el proyecto generado directamente en VS Code desde el Builder Platform.

### Endpoint

`POST /api/products/{id}/open-vscode`

**Path safety**: `Path.GetFullPath(path).StartsWith(Path.GetFullPath(outputPath) + DirectorySeparatorChar)` — case-insensitive, usa `Scaffold:OutputPath` de `IConfiguration`. Rechaza si el producto no tiene projectPath o si no está dentro del OutputPath. Devuelve 422 si VS Code no está instalado.

**Detección de VS Code**: `cmd.exe /c where code` — exit 0 = encontrado; exit ≠ 0 → 422 con instrucciones de instalación.

**Launch**: `cmd.exe /c code "<path>"` — fire-and-forget. VS Code permanece abierto; el proceso cmd termina.

**Activity events nuevos**: `VSCodeOpenRequested`, `VSCodeOpenSucceeded`, `VSCodeOpenFailed`.

**Nota técnica**: `using System.Diagnostics;` removido del controller — colisiona con `BuilderPlatform.Domain.Entities.ActivityEvent`. Usar `System.Diagnostics.Process` y `System.Diagnostics.ProcessStartInfo` fully qualified.

### UI — 3 entry points

1. **Header del workspace**: botón "VS Code" cuando `scaffoldStatus === "complete"` y `projectPath` existe
2. **Tab Preview** (`PreviewPanel`): botón "VS Code" en la barra de acciones, junto a Start/Stop/Abrir SaaS
3. **Tab Scaffold** (`ScaffoldTree`): botón "Abrir en VS Code" en la stats bar, junto al path del proyecto

Estado compartido: `vsCodeLoading` (spinner mientras lanza) + `vsCodeError` (mensaje amarillo si no está instalado).

**E2E validado** (2026-05-19):
- VS Code no instalado → 422 + mensaje instructivo en UI ✓
- VS Code instalado → VS Code abre el proyecto ✓
- dotnet build 0 errores ✓ · tsc 0 errores ✓ · next build limpio ✓

---

## Sprint 18 — Generated Product Auth & Data Persistence MVP

**Objetivo**: Transición de productos generados de "demo-ready visual" a "usable con login y datos persistentes".

### Auth system generado

`middleware.ts` en la raíz del frontend generado — protege todas las rutas excepto `/login` y `/api/auth/*`:
- Redirect 307 a `/login` para páginas no autenticadas
- 401 JSON para API calls no autenticadas
- Redirect a `/dashboard` si cookie válida intenta acceder a `/login`

Cookie `bp-session` (httpOnly, 7 días, sameSite: lax) — seteada por `/api/auth/login`, limpiada por `/api/auth/logout`.

### Demo credentials

`demoEmail = admin@{slug}.com` / `demoPassword = "Demo1234!"` — generadas en `ScaffoldEngine.GenerateFrontend()` y embedidas en `LoginPage`, `ApiAuthLoginRoute`. Visibles en la pantalla de login (credentials box encima del form).

### JSON persistence

`/api/data/[module]/route.ts` — dynamic route handler:
- `GET`: lee `.data/{module}.json` (cwd-relative), devuelve `[]` si no existe
- `POST`: append record con `id: Date.now()`, `_createdAt: ISO`, persiste en `.data/{module}.json`
- Sanitiza nombre del módulo: `/[^a-z0-9-]/g` + `slice(0, 50)` contra path traversal
- **Next.js 15**: `params` es `Promise<{module: string}>` — requiere `await context.params`

`.data/` en `.gitignore` generado — los archivos de datos no se versionan.

### AppShell + Server/Client boundary

`AppShell.tsx` (client component) recibe `sidebar` como `React.ReactNode` desde el server `RootLayout`. Patrón necesario porque Sidebar es server component y AppShell necesita `usePathname` para ocultar sidebar en `/login`.

```tsx
// RootLayout (server):
<AppShell sidebar={<Sidebar />}>{children}</AppShell>

// AppShell (client):
if (pathname === "/login") return <>{children}</>  // sin sidebar
```

### CRUD Modal en feature pages

Cada feature page generada incluye:
- `savedRows` state cargado desde `/api/data/{route}` en mount (useEffect + useCallback)
- Botón "Nuevo {Title}" en el header del módulo
- Modal con form: un `<input>` por columna (excepto la columna de status)
- POST a `/api/data/{route}` → reload
- Demo rows se renderizan al 40% de opacidad con separador "Datos demo" cuando hay savedRows

### Logout

Sidebar incluye botón "Cerrar sesión" (bottom): `POST /api/auth/logout` → `window.location.href = "/login"`.

**E2E validado** (2026-05-20):
- Unauthenticated /dashboard → 307 /login ✓
- Login admin@s18restaurantauth.com / Demo1234! → 200 ok + cookie ✓
- Authenticated /dashboard → 200 ✓
- Unauthenticated /api/data/mesas → 401 ✓
- POST record → 201 + JSON en disco ✓
- GET después de POST → record persiste ✓
- Logout → cookie cleared → /dashboard → 307 /login ✓
- `tsc --noEmit` 0 errores ✓ · `next build` limpio (13 rutas) ✓

---

## Deuda técnica activa

| Item | Prioridad | Descripción |
|------|-----------|-------------|
| Fresh product E2E entity-labels | Baja | Fix Sprint 12 pendiente confirmar en producto nuevo completamente fresh |
| Templates más ricos por industria | Media | Gaming, Healthcare, Logistics tienen templates básicos vs Restaurant |
| Reservaciones en Restaurant | Baja | No aparece en CoreFeatures.Take(5) en algunos productos |
| Pluralización irregular | Baja | Category→categories, Company→companies no manejado en ResolveRoute |
| dashboard_premium idempotencia | Baja | Se aplica en cada request sin check |
| SSE step coverage en HandleValidation/HandleDeploy | Baja | Pings OK, pero sin StepAsync en esos flujos |
| Auth multi-user / roles | Media | Actualmente single demo user — un solo credential hardcoded por producto |
| CRUD delete action | Baja | Solo create + read; no hay delete de registros persistidos |

## Regla de oro del Builder Platform

> **Un SaaS generado no está demo-ready si sus módulos siguen siendo CRUD genéricos.**

La persona que abre el producto generado debe entender el negocio, las operaciones y los flujos en 5 segundos — sin leer documentación. Columnas operacionales reales + datos coherentes entre módulos + estados semánticos del dominio = product realism.
