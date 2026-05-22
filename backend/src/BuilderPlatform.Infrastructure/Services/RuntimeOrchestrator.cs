using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace BuilderPlatform.Infrastructure.Services;

public record RuntimeWork(string Type, Guid ProductId, string? Payload = null);

public class RuntimeOrchestrator(IServiceScopeFactory scopeFactory, ILogger<RuntimeOrchestrator> logger, ScaffoldEngine scaffoldEngine, ProjectAwarenessEngine awarenessEngine, RuntimePatchEngine patchEngine, PreviewRunner previewRunner, RuntimeValidationEngine validationEngine, AutofixEngine autofixEngine, DeployEngine deployEngine, RuntimeEventBus bus, ProductEvolutionService evolutionService, RefactorDetectionService refactorService)
    : BackgroundService
{
    private readonly Channel<RuntimeWork> _queue =
        Channel.CreateUnbounded<RuntimeWork>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(RuntimeWork work) => _queue.Writer.TryWrite(work);

    public override async Task StartAsync(CancellationToken ct)
    {
        // Clean up stale state from a previous crashed instance
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staleRuns = await db.ValidationRuns.Where(r => r.Status == "running").ToListAsync(ct);
        foreach (var run in staleRuns)
        {
            run.Status      = "failed";
            run.FinishedAt  = DateTime.UtcNow;
            run.Errors      = "Backend restarted — run aborted";
        }
        var staleProducts = await db.Products.Where(p => p.IsProcessing).ToListAsync(ct);
        foreach (var p in staleProducts)
        {
            p.IsProcessing  = false;
            if (p.RuntimeHealth == "recovering") p.RuntimeHealth = "degraded";
        }
        if (staleRuns.Count > 0 || staleProducts.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Startup cleanup: aborted {Runs} stale runs, reset {Products} stale products",
                staleRuns.Count, staleProducts.Count);
        }
        await previewRunner.StartupCleanupAsync();
        await base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var work in _queue.Reader.ReadAllAsync(ct))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await HandleWork(work, db, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "RuntimeOrchestrator error — type={Type} product={Id}", work.Type, work.ProductId);
            }
        }
    }

    private Task HandleWork(RuntimeWork work, AppDbContext db, CancellationToken ct) => work.Type switch
    {
        "init"              => RunInitPipeline(work.ProductId, db, ct),
        "approval_resolved" => HandleApprovalResolved(work.ProductId, work.Payload, db, ct),
        "message"           => HandleMessageIntent(work.ProductId, work.Payload, db, ct),
        "scaffold"          => HandleScaffold(work.ProductId, db, ct),
        "feature_execution" => HandleFeatureExecution(work.ProductId, work.Payload, db, ct),
        "dashboard_update"  => HandleDashboardUpdate(work.ProductId, work.Payload, db, ct),
        "ui_evolution"      => HandleUiEvolution(work.ProductId, work.Payload, db, ct),
        "validation"        => HandleValidation(work.ProductId, db, ct),
        "deploy"            => HandleDeploy(work.ProductId, db, ct),
        _                   => Task.CompletedTask,
    };

    // ── Init Pipeline ──────────────────────────────────────────────────────────

    private async Task RunInitPipeline(Guid productId, AppDbContext db, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([productId], ct);
        if (product is null || product.Status != ProductStatus.Draft) return;

        var profile = ContentGenerator.Analyze(product.Name, product.Prompt);

        // Stage 0 — Discovering
        await SetProcessing(product, ProductStatus.Discovering, "discovery", db);
        AddActivity(db, productId, ActivityType.DiscoveryStarted, "Discovery iniciado",
            $"Analizando: {product.Prompt[..Math.Min(product.Prompt.Length, 80)]}");
        AddRuntimeMessage(db, productId, "Entendido. Estoy analizando tu idea. Iniciando proceso de discovery para identificar el dominio, usuarios y features clave...");
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);

        await Task.Delay(3500, ct);

        // Stage 1 — Brief artifact generated
        product = await db.Products.FindAsync([productId], ct);
        if (product is null) return;

        SaveMemory(db, productId, "industry",  profile.Industry);
        SaveMemory(db, productId, "saas_type", profile.SaasType);
        SaveMemory(db, productId, "features",  string.Join("|", profile.CoreFeatures));

        var briefContent   = ArtifactContentGenerator.GenerateBrief(product.Name, profile);
        var briefArtifact  = await UpsertArtifact(db, productId, "brief", $"Product Brief: {product.Name}", briefContent);

        SaveMemory(db, productId, "artifact_brief_id", briefArtifact.Id.ToString());

        AddActivity(db, productId, ActivityType.BriefGenerated, "Brief generado",
            $"{profile.IndustryLabel} · {profile.SaasType} · {profile.CoreFeatures.Length} features",
            briefArtifact.Id);
        AddRuntimeMessage(db, productId, ContentGenerator.GenerateRuntimeChatBrief(product.Name, profile));
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);

        await Task.Delay(4000, ct);

        // Stage 2 — Architecture + DB Schema artifacts
        product = await db.Products.FindAsync([productId], ct);
        if (product is null) return;

        await SetProcessing(product, ProductStatus.Architecting, "architecting", db);
        await db.SaveChangesAsync(ct);

        await Task.Delay(1500, ct);

        var archContent    = ArtifactContentGenerator.GenerateArchitecture(product.Name, profile);
        var archArtifact   = await UpsertArtifact(db, productId, "architecture", $"Arquitectura del Sistema: {product.Name}", archContent);

        var schemaContent  = ArtifactContentGenerator.GenerateDbSchema(product.Name, profile);
        var schemaArtifact = await UpsertArtifact(db, productId, "db_schema", $"Esquema DB: {product.Name}", schemaContent);

        SaveMemory(db, productId, "db_entities",        string.Join("|", profile.DbEntities));
        SaveMemory(db, productId, "artifact_arch_id",   archArtifact.Id.ToString());
        SaveMemory(db, productId, "artifact_schema_id", schemaArtifact.Id.ToString());

        AddActivity(db, productId, ActivityType.ArchitectureGenerated, "Arquitectura generada",
            $"Stack: .NET 9 + SQL Server + Next.js 15 · {profile.DbEntities.Length} entidades",
            archArtifact.Id);

        AddActivity(db, productId, ActivityType.ArtifactGenerated, "DB Schema generado",
            $"{profile.DbEntities.Length} entidades · índices y relaciones definidos",
            schemaArtifact.Id);

        // Architecture approval linked to artifact
        var archApproval = new Approval
        {
            ProductId   = productId,
            ArtifactId  = archArtifact.Id,
            Title       = "Aprobar arquitectura del sistema",
            Description = $"El runtime generó la arquitectura para **{product.Name}** ({profile.IndustryLabel}). " +
                          $"Patrón: {profile.ArchitecturePattern.Split('.').First()}. " +
                          $"Entidades DB: {string.Join(", ", profile.DbEntities.Take(5))}. " +
                          "Revisá el artifact de arquitectura antes de continuar.",
        };
        db.Approvals.Add(archApproval);

        AddRuntimeMessage(db, productId, ContentGenerator.GenerateRuntimeChatArchitecture(product.Name, profile));

        product.IsProcessing = false;
        product.RuntimePhase = "waiting_approval";
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
    }

    // ── Approval resolved ──────────────────────────────────────────────────────

    private async Task HandleApprovalResolved(Guid productId, string? payload, AppDbContext db, CancellationToken ct)
    {
        if (!bool.TryParse(payload, out var approved)) return;

        var product = await db.Products.FindAsync([productId], ct);
        if (product is null) return;

        if (!approved)
        {
            AddRuntimeMessage(db, productId, "Entendido. Rechazaste la propuesta. Podés indicarme qué cambiar y regeneraré la arquitectura o el plan de sprints.");
            product.RuntimePhase = "idle";
            product.IsProcessing = false;
            await db.SaveChangesAsync(ct);
            return;
        }

        if (product.RuntimePhase != "waiting_approval") return;

        switch (product.Status)
        {
            case ProductStatus.Architecting:
                await ContinueToPlanning(product, productId, db, ct);
                break;
            case ProductStatus.Planning:
                await ContinueToBuilding(product, productId, db, ct);
                break;
        }
    }

    private async Task ContinueToPlanning(Product product, Guid productId, AppDbContext db, CancellationToken ct)
    {
        var profile = ContentGenerator.Analyze(product.Name, product.Prompt);

        await SetProcessing(product, ProductStatus.Planning, "planning", db);
        await db.SaveChangesAsync(ct);

        await Task.Delay(3000, ct);

        var roadmapContent  = ArtifactContentGenerator.GenerateRoadmap(product.Name, profile);
        var roadmapArtifact = await UpsertArtifact(db, productId, "roadmap", $"MVP Roadmap: {product.Name}", roadmapContent);

        var sprintContent   = ArtifactContentGenerator.GenerateSprintPlan(product.Name, profile, 1);
        var sprintArtifact  = await UpsertArtifact(db, productId, "sprint_plan", $"Sprint 1 Plan: {product.Name}", sprintContent);

        SaveMemory(db, productId, "roadmap",              roadmapContent[..Math.Min(roadmapContent.Length, 500)]);
        SaveMemory(db, productId, "sprint_count",         profile.SprintPlan.Length.ToString());
        SaveMemory(db, productId, "artifact_roadmap_id",  roadmapArtifact.Id.ToString());
        SaveMemory(db, productId, "artifact_sprint_id",   sprintArtifact.Id.ToString());

        AddActivity(db, productId, ActivityType.SprintStarted, "Sprint planning generado",
            $"{profile.SprintPlan.Length} sprints · {profile.SprintPlan[0].Split(":")[0]}",
            roadmapArtifact.Id);

        AddActivity(db, productId, ActivityType.ArtifactGenerated, "Sprint 1 Plan generado",
            profile.SprintPlan[0].Split(":").LastOrDefault()?.Trim(),
            sprintArtifact.Id);

        var roadmapApproval = new Approval
        {
            ProductId   = productId,
            ArtifactId  = roadmapArtifact.Id,
            Title       = "Aprobar roadmap y comenzar construcción",
            Description = $"Sprint planning listo para **{product.Name}**. " +
                          $"{string.Join(" | ", profile.SprintPlan.Select(s => s.Split(":")[0]))}. " +
                          "Al aprobar, el runtime iniciará el Sprint 1.",
        };
        db.Approvals.Add(roadmapApproval);

        AddActivity(db, productId, ActivityType.ApprovalPending, "Esperando aprobación de roadmap", null);
        AddRuntimeMessage(db, productId, ContentGenerator.GenerateRuntimeChatRoadmap(profile));

        product.IsProcessing = false;
        product.RuntimePhase = "waiting_approval";
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
    }

    private async Task ContinueToBuilding(Product product, Guid productId, AppDbContext db, CancellationToken ct)
    {
        await SetProcessing(product, ProductStatus.Building, "building", db);
        await db.SaveChangesAsync(ct);

        await Task.Delay(2000, ct);

        var profile = ContentGenerator.Analyze(product.Name, product.Prompt);

        AddActivity(db, productId, ActivityType.SprintStarted, "Sprint 1 iniciado",
            profile.SprintPlan[0].Split(":").LastOrDefault()?.Trim());
        AddRuntimeMessage(db, productId, ContentGenerator.GenerateRuntimeChatBuilding(product.Name));

        SaveMemory(db, productId, "current_sprint",   "1");
        SaveMemory(db, productId, "build_started_at", DateTime.UtcNow.ToString("O"));

        product.IsProcessing = false;
        product.RuntimePhase = "building";
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);

        // Kick off scaffold generation asynchronously (next queue item)
        Enqueue(new RuntimeWork("scaffold", productId));
    }

    // ── Scaffold generation ────────────────────────────────────────────────────

    private async Task HandleScaffold(Guid productId, AppDbContext db, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([productId], ct);
        if (product is null || product.ScaffoldStatus == "complete") return;

        var profile = ContentGenerator.Analyze(product.Name, product.Prompt);

        // ── Step 1: announce scaffold start ───────────────────────────────────
        product.ScaffoldStatus = "generating";
        product.IsProcessing   = true;
        product.RuntimePhase   = "scaffolding";
        product.UpdatedAt      = DateTime.UtcNow;

        AddActivity(db, productId, ActivityType.ScaffoldStarted, "Iniciando scaffold del proyecto",
            $"{profile.DbEntities.Length} entidades · {profile.CoreFeatures.Length} módulos · Clean Architecture");
        AddRuntimeMessage(db, productId, ContentGenerator.GenerateRuntimeChatScaffolding(product.Name, profile));
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
        await bus.StepAsync(productId, "Generando backend (.NET 9)...");

        await Task.Delay(2000, ct);

        // ── Step 2: backend ───────────────────────────────────────────────────
        AddActivity(db, productId, ActivityType.ScaffoldStarted, "Generando scaffold backend",
            $".NET 9 · {Math.Min(profile.DbEntities.Length, 8)} entidades · {Math.Min(profile.DbEntities.Length, 5)} controllers");
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
        await bus.StepAsync(productId, "Generando frontend (Next.js 15)...");

        await Task.Delay(2500, ct);

        // ── Step 3: frontend ──────────────────────────────────────────────────
        AddActivity(db, productId, ActivityType.ScaffoldStarted, "Generando scaffold frontend",
            $"Next.js 15 · {Math.Min(profile.CoreFeatures.Length, 5)} módulos · Operational Dark design system");
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
        await bus.StepAsync(productId, "Escribiendo archivos en disco...");

        await Task.Delay(2000, ct);

        // ── Step 4: actually generate files on disk ───────────────────────────
        try
        {
            var (projectPath, entries) = await scaffoldEngine.GenerateAsync(product, profile, ct);

            foreach (var entry in entries)
            {
                entry.ProductId = productId;
                db.ScaffoldEntries.Add(entry);
            }

            product.ProjectPath    = projectPath;
            product.ScaffoldStatus = "complete";
            product.IsProcessing   = false;
            product.RuntimePhase   = "building";
            product.UpdatedAt      = DateTime.UtcNow;

            var fileCount = entries.Count(e => e.EntryType == "file");
            AddActivity(db, productId, ActivityType.ScaffoldCompleted, "Scaffold completado",
                $"{fileCount} archivos generados · {projectPath}");
            AddRuntimeMessage(db, productId,
                ContentGenerator.GenerateRuntimeChatScaffoldComplete(product.Name, fileCount, projectPath));

            SaveMemory(db, productId, "scaffold_path",       projectPath);
            SaveMemory(db, productId, "scaffold_file_count", fileCount.ToString());

            // ── Scan project and build module registry ─────────────────────────
            var modules = await awarenessEngine.ScanAndRegisterAsync(product, db, ct);
            AddActivity(db, productId, ActivityType.ProjectScanned, "Estructura del proyecto escaneada",
                $"{modules.Count} módulo(s) registrados en el registry");
            SaveMemory(db, productId, "module_count", modules.Count.ToString());

            // ── Initialize evolution memory ─────────────────────────────────────
            evolutionService.RecordScaffold(productId, profile.Industry, modules.ToList(), db);
            AddActivity(db, productId, ActivityType.EvolutionMemoryUpdated,
                "Evolution memory inicializada",
                $"{modules.Count} módulo(s) registrados en evolution memory");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scaffold generation failed for product {Id}", productId);
            product.ScaffoldStatus = "error";
            product.IsProcessing   = false;
            product.RuntimePhase   = "building";
            product.UpdatedAt      = DateTime.UtcNow;
            AddActivity(db, productId, ActivityType.ErrorOccurred, "Error en scaffold",
                ex.Message.Length > 150 ? ex.Message[..150] : ex.Message);
        }

        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
    }

    // ── Message intent handler ─────────────────────────────────────────────────

    private async Task HandleMessageIntent(Guid productId, string? content, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var product = await db.Products.FindAsync([productId], ct);
        if (product is null) return;

        var intent  = IntentEngine.Classify(content);
        var profile = ContentGenerator.Analyze(product.Name, product.Prompt);

        var lastMsg = await db.ChatMessages
            .Where(m => m.ProductId == productId && m.Role == MessageRole.User)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (lastMsg is not null)
        {
            lastMsg.DetectedIntent = IntentEngine.ToLabel(intent.Intent);
            lastMsg.Confidence     = intent.Confidence;
        }

        SaveMemory(db, productId, "last_intent", $"{IntentEngine.ToLabel(intent.Intent)}:{intent.Confidence:F2}");

        string response = intent.Intent switch
        {
            Intent.FeatureRequest    => ContentGenerator.GenerateFeatureResponse(content, profile),
            Intent.DeploymentRequest => ContentGenerator.GenerateDeployResponse(),
            Intent.BugFix            => ContentGenerator.GenerateBugFixResponse(content),
            Intent.UiRefinement      => ContentGenerator.GenerateUiResponse(),
            Intent.DashboardRequest  => ContentGenerator.GenerateDashboardUpdateStart(ExtractWidgetName(content), product.Name),
            Intent.UiEvolution       => ContentGenerator.GenerateUiEvolutionStart(content),
            Intent.ValidateRequest   => ContentGenerator.GenerateValidationStart(),
            _                        => ContentGenerator.GenerateUnknownResponse(product.Status.ToString()),
        };

        var placeholder = await db.ChatMessages
            .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (placeholder is not null)
            placeholder.Content = response;
        else
            AddRuntimeMessage(db, productId, response);

        if (intent.Intent == Intent.DashboardRequest)
        {
            var widgetName = ExtractWidgetName(content);
            AddActivity(db, productId, ActivityType.ScaffoldDeltaStarted, $"Dashboard update solicitado: {widgetName}",
                content.Length > 80 ? content[..80] + "…" : content);

            if (product.ScaffoldStatus == "complete" && !string.IsNullOrWhiteSpace(product.ProjectPath))
            {
                var dashMsg = await db.ChatMessages
                    .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync(ct);
                if (dashMsg is not null)
                    dashMsg.Content = ContentGenerator.GenerateDashboardUpdateStart(widgetName, product.Name);

                product.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                Enqueue(new RuntimeWork("dashboard_update", productId, widgetName));
                return;
            }
        }
        else if (intent.Intent == Intent.DeploymentRequest)
        {
            db.Approvals.Add(new Approval
            {
                ProductId   = productId,
                Title       = "Solicitud de deploy",
                Description = "Verificaré build limpio, tests pasando y configuración de entorno. ¿Confirmás el deploy a staging?",
            });
            AddActivity(db, productId, ActivityType.ApprovalPending, "Solicitud de deploy pendiente", "El usuario solicitó deploy a staging");
        }
        else if (intent.Intent == Intent.FeatureRequest)
        {
            var featureName = ExtractFeatureName(content);
            AddActivity(db, productId, ActivityType.FeatureDetected, $"Feature detectada: {featureName}",
                content.Length > 80 ? content[..80] + "…" : content);

            // Trigger incremental scaffold delta if the project scaffold is ready
            if (product.ScaffoldStatus == "complete" && !string.IsNullOrWhiteSpace(product.ProjectPath))
            {
                // Update placeholder message to indicate execution is starting
                var execMsg = await db.ChatMessages
                    .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync(ct);
                if (execMsg is not null)
                    execMsg.Content = ContentGenerator.GenerateFeatureExecutionStart(featureName, product.Name);

                product.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                Enqueue(new RuntimeWork("feature_execution", productId, featureName));
                return;
            }
        }
        else if (intent.Intent == Intent.UiEvolution)
        {
            var op = RuntimePatchEngine.ClassifyPatch(content);
            AddActivity(db, productId, ActivityType.RuntimePatchStarted,
                $"UI evolution: {op}", content.Length > 80 ? content[..80] + "…" : content);

            if (product.ScaffoldStatus == "complete" && !string.IsNullOrWhiteSpace(product.ProjectPath))
            {
                var evoMsg = await db.ChatMessages
                    .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync(ct);
                if (evoMsg is not null)
                    evoMsg.Content = ContentGenerator.GenerateUiEvolutionStart(content);

                product.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                Enqueue(new RuntimeWork("ui_evolution", productId, content));
                return;
            }
        }
        else if (intent.Intent == Intent.ValidateRequest)
        {
            if (product.ScaffoldStatus == "complete" && !string.IsNullOrWhiteSpace(product.ProjectPath))
            {
                var valMsg = await db.ChatMessages
                    .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync(ct);
                if (valMsg is not null)
                    valMsg.Content = ContentGenerator.GenerateValidationStart();

                product.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                Enqueue(new RuntimeWork("validation", productId));
                return;
            }
        }
        else if (intent.Intent == Intent.BugFix)
        {
            AddActivity(db, productId, ActivityType.ErrorOccurred, "Reporte de bug recibido",
                content.Length > 80 ? content[..80] + "…" : content);
        }

        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
    }

    // ── Feature execution (Bundle) ─────────────────────────────────────────────

    private async Task HandleFeatureExecution(Guid productId, string? featureName, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(featureName)) return;

        var product = await db.Products.FindAsync([productId], ct);
        if (product is null || product.ScaffoldStatus != "complete" || string.IsNullOrWhiteSpace(product.ProjectPath))
            return;

        var alreadyRegistered = await awarenessEngine.ModuleExistsAsync(productId, featureName!, db, ct);

        product.IsProcessing = true;
        product.RuntimePhase = "executing";
        product.UpdatedAt    = DateTime.UtcNow;
        AddActivity(db, productId, ActivityType.ScaffoldDeltaStarted, $"Feature bundle iniciado: {featureName}",
            "Generando módulo completo: entidad, controller, página, nav, registry, widget");
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
        await bus.StepAsync(productId, $"Generando módulo: {featureName}...");

        await Task.Delay(2000, ct);

        var navAdded    = false;
        var widgetAdded = false;
        var codeCreated = 0;

        // ── Evolution context (read once before generation) ────────────────────
        var evolutionCtx = await evolutionService.GetEvolutionContextAsync(productId, db);
        var relations    = evolutionService.DetectRelations(featureName!, evolutionCtx);

        if (relations.Count > 0)
        {
            var connectedWith = string.Join(", ", relations.Select(r => r.To));
            AddActivity(db, productId, ActivityType.EvolutionRelationsDetected,
                $"Relaciones detectadas: {featureName} ↔ {connectedWith}",
                string.Join(" | ", relations.Select(r => $"{r.RelationType}: {r.Reason}")));
        }

        try
        {
            // ── 1. Code scaffold (entity + controller + page) ──────────────────
            var codeChanges = await scaffoldEngine.GenerateDeltaAsync(product, featureName!, ct);
            foreach (var c in codeChanges) db.ScaffoldChanges.Add(c);

            codeCreated = codeChanges.Count(c => c.ChangeType == "created");
            var backendFiles  = codeChanges.Where(c => c.Layer == "backend"  && c.ChangeType == "created").ToList();
            var frontendFiles = codeChanges.Where(c => c.Layer == "frontend" && c.ChangeType == "created").ToList();

            if (backendFiles.Count  > 0)
                AddActivity(db, productId, ActivityType.BackendModuleGenerated,  $"Backend generado: {featureName}",
                    string.Join(", ", backendFiles.Select(c => Path.GetFileName(c.TargetPath))));
            if (frontendFiles.Count > 0)
                AddActivity(db, productId, ActivityType.FrontendModuleGenerated, $"Frontend generado: {featureName}",
                    string.Join(", ", frontendFiles.Select(c => Path.GetFileName(c.TargetPath))));

            await bus.StepAsync(productId, $"Código generado — actualizando navegación...");

            // ── 2. Navigation registry ─────────────────────────────────────────
            var route   = ToDeltaRoute(featureName!);
            var navPath = Path.Combine(product.ProjectPath!, "frontend", "registry", "nav-items.json");
            navAdded    = await awarenessEngine.CheckAndUpdateNavAsync(product.ProjectPath!, $"/{route}", featureName!, ct);
            db.ScaffoldChanges.Add(new ScaffoldChange
            {
                ProductId   = productId,
                ChangeType  = navAdded ? "created" : "skipped",
                TargetPath  = navPath,
                ModuleLabel = featureName!,
                Layer       = "navigation",
            });
            if (navAdded)
                AddActivity(db, productId, ActivityType.NavigationItemAdded,
                    $"Nav item agregado: {featureName}", $"/{route} → nav-items.json");

            // ── 3. Dashboard widget (optional, if dashboard exists) ────────────
            var (hasDashboard, widgetExists) = await awarenessEngine.CheckDashboardAsync(product.ProjectPath!, featureName!, ct);
            if (hasDashboard)
            {
                if (!widgetExists)
                {
                    var widgetPath = await scaffoldEngine.GenerateWidgetAsync(product, featureName!, ct);
                    if (!string.IsNullOrWhiteSpace(widgetPath))
                    {
                        var relPath = Path.GetRelativePath(product.ProjectPath!, widgetPath).Replace('\\', '/');
                        await awarenessEngine.AddWidgetToRegistryAsync(product.ProjectPath!, featureName!, relPath, ct);
                        widgetAdded = true;
                        db.ScaffoldChanges.Add(new ScaffoldChange
                        {
                            ProductId   = productId,
                            ChangeType  = "created",
                            TargetPath  = widgetPath,
                            ModuleLabel = featureName!,
                            Layer       = "dashboard",
                        });
                        AddActivity(db, productId, ActivityType.DashboardWidgetAdded,
                            $"Dashboard widget: {featureName}", Path.GetFileName(widgetPath));
                    }
                }
                else
                {
                    db.ScaffoldChanges.Add(new ScaffoldChange
                    {
                        ProductId   = productId,
                        ChangeType  = "skipped",
                        TargetPath  = string.Empty,
                        ModuleLabel = featureName!,
                        Layer       = "dashboard",
                    });
                }
            }

            await bus.StepAsync(productId, $"Actualizando registry y dashboard...");

            // ── 4. Module registry (modules.json) ──────────────────────────────
            await awarenessEngine.RegisterDeltaModuleAsync(product, featureName!, db, codeChanges, ct);
            var regPath = Path.Combine(product.ProjectPath!, "frontend", "registry", "modules.json");
            db.ScaffoldChanges.Add(new ScaffoldChange
            {
                ProductId   = productId,
                ChangeType  = alreadyRegistered ? "skipped" : "created",
                TargetPath  = regPath,
                ModuleLabel = featureName!,
                Layer       = "registry",
            });
            AddActivity(db, productId, ActivityType.RegistryUpdated,
                $"Registry actualizado: {featureName}",
                "modules.json + nav-items.json + dashboard.json");

            // ── 5. Artifact ────────────────────────────────────────────────────
            var artifactContent = BuildFeatureArtifactContent(featureName!, product.Name, codeChanges);
            var artifact        = await UpsertArtifact(db, productId, "feature_module", $"Módulo: {featureName}", artifactContent);
            AddActivity(db, productId, ActivityType.RuntimeReviewCompleted,
                $"Bundle completado: {featureName}",
                $"code={codeCreated} · nav={navAdded} · widget={widgetAdded}", artifact.Id);

            // ── 6. Runtime message (evolution-aware) ───────────────────────────
            var lastMsg = await db.ChatMessages
                .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var msgContent = alreadyRegistered
                ? ContentGenerator.GenerateEvolutionAwareStart(featureName!, product.Name, relations)
                : ContentGenerator.GenerateEvolutionBundleComplete(featureName!, product.Name, relations, codeCreated, navAdded, widgetAdded);

            if (lastMsg is not null) lastMsg.Content = msgContent;
            else AddRuntimeMessage(db, productId, msgContent);

            // ── 7. Record in evolution memory ──────────────────────────────────
            evolutionService.RecordFeature(productId, featureName!, route, relations, evolutionCtx, db);
            AddActivity(db, productId, ActivityType.EvolutionMemoryUpdated,
                $"Evolution memory actualizada: {featureName}",
                relations.Count > 0
                    ? $"Conectado con: {string.Join(", ", relations.Select(r => r.To))}"
                    : "Módulo independiente registrado");

            // ── 8. Refactor detection (after memory update) ────────────────────
            // evolutionCtx was mutated in-place by RecordFeature (new module/relations added)
            // so we can detect against it directly without re-reading from DB
            var newRecs = await refactorService.DetectAndPersistAsync(productId, evolutionCtx, db);
            if (newRecs.Count > 0)
            {
                AddActivity(db, productId, ActivityType.RefactorDetected,
                    $"{newRecs.Count} recomendación(es) arquitectónica(s) detectada(s)",
                    string.Join(" | ", newRecs.Select(r => $"[{r.Severity}] {r.Title}")));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Feature bundle failed for product {Id}", productId);
            AddActivity(db, productId, ActivityType.ErrorOccurred, $"Error en feature bundle: {featureName}",
                ex.Message.Length > 150 ? ex.Message[..150] : ex.Message);
        }

        product.IsProcessing = false;
        product.RuntimePhase = "building";
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
    }

    // ── Dashboard update ───────────────────────────────────────────────────────

    private async Task HandleDashboardUpdate(Guid productId, string? widgetName, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(widgetName)) return;

        var product = await db.Products.FindAsync([productId], ct);
        if (product is null || string.IsNullOrWhiteSpace(product.ProjectPath)) return;

        var (hasDashboard, widgetExists) = await awarenessEngine.CheckDashboardAsync(product.ProjectPath, widgetName, ct);

        if (!hasDashboard)
        {
            var noMsg = await db.ChatMessages
                .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (noMsg is not null)
                noMsg.Content = ContentGenerator.GenerateDashboardNoDashboard(product.Name);
            product.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        product.IsProcessing = true;
        product.RuntimePhase = "executing";
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await Task.Delay(1500, ct);

        string widgetPath = string.Empty;

        if (!widgetExists)
        {
            widgetPath = await scaffoldEngine.GenerateWidgetAsync(product, widgetName, ct);

            if (!string.IsNullOrWhiteSpace(widgetPath))
            {
                var relPath = Path.GetRelativePath(product.ProjectPath, widgetPath).Replace('\\', '/');
                await awarenessEngine.AddWidgetToRegistryAsync(product.ProjectPath, widgetName, relPath, ct);

                db.ScaffoldChanges.Add(new ScaffoldChange
                {
                    ProductId   = productId,
                    ChangeType  = "created",
                    TargetPath  = widgetPath,
                    ModuleLabel = widgetName,
                    Layer       = "dashboard",
                });

                AddActivity(db, productId, ActivityType.DashboardWidgetAdded,
                    $"Widget generado: {widgetName}",
                    $"{Path.GetFileName(widgetPath)} · dashboard.json actualizado");

                AddActivity(db, productId, ActivityType.RegistryUpdated,
                    "Dashboard registry actualizado",
                    $"widget '{widgetName}' agregado a dashboard.json");
            }
        }
        else
        {
            db.ScaffoldChanges.Add(new ScaffoldChange
            {
                ProductId   = productId,
                ChangeType  = "skipped",
                TargetPath  = string.Empty,
                ModuleLabel = widgetName,
                Layer       = "dashboard",
            });
            AddActivity(db, productId, ActivityType.DashboardWidgetAdded,
                $"Widget ya existe: {widgetName}",
                "Idempotente — sin cambios en dashboard registry");
        }

        var lastMsg = await db.ChatMessages
            .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var msgContent = widgetExists
            ? ContentGenerator.GenerateDashboardWidgetExists(widgetName, product.Name)
            : ContentGenerator.GenerateDashboardUpdateComplete(widgetName, product.Name, Path.GetFileName(widgetPath));

        if (lastMsg is not null) lastMsg.Content = msgContent;
        else AddRuntimeMessage(db, productId, msgContent);

        product.IsProcessing = false;
        product.RuntimePhase = "building";
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
    }

    // ── UI Evolution ──────────────────────────────────────────────────────────

    private async Task HandleUiEvolution(Guid productId, string? content, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var product = await db.Products.FindAsync([productId], ct);
        if (product is null || string.IsNullOrWhiteSpace(product.ProjectPath)) return;

        var op = RuntimePatchEngine.ClassifyPatch(content);

        if (op == PatchOperation.Unknown)
        {
            var unknownMsg = await db.ChatMessages
                .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var skipReason = "No se reconoció una operación de UI evolution válida para este proyecto.";
            if (unknownMsg is not null) unknownMsg.Content = ContentGenerator.GeneratePatchSkipped(skipReason);
            AddActivity(db, productId, ActivityType.RuntimePatchSkipped, "UI evolution: operación no reconocida", content[..Math.Min(content.Length, 80)]);
            product.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        product.IsProcessing = true;
        product.RuntimePhase = "patching";
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await Task.Delay(1500, ct);

        PatchResult result;
        try
        {
            result = await patchEngine.ApplyPatchAsync(product, op, db, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UI evolution patch failed for product {Id}", productId);
            result = new PatchResult(false, ex.Message.Length > 150 ? ex.Message[..150] : ex.Message);
        }

        var previewRestarted = false;

        if (result.Success)
        {
            AddActivity(db, productId, ActivityType.RuntimeFileUpdated,
                $"Archivo actualizado: {Path.GetFileName(result.FilePath ?? "—")}",
                result.Message);

            // Auto-restart preview if running
            var liveStatus = previewRunner.GetLiveStatus(productId);
            if (liveStatus.status == "running")
            {
                await previewRunner.StopAsync(productId);
                previewRunner.StartAsync(productId);
                previewRestarted = true;
                AddActivity(db, productId, ActivityType.PreviewRestarted,
                    "Preview reiniciado automáticamente", "Patch aplicado — preview recargado");
            }
        }
        else
        {
            var actType = result.Message.StartsWith("ℹ") || result.Success == false && !result.Message.Contains("Error")
                ? ActivityType.RuntimePatchSkipped
                : ActivityType.RuntimePatchFailed;
            AddActivity(db, productId, actType, $"UI evolution: {op}", result.Message);
        }

        // Update last runtime message
        var lastMsg = await db.ChatMessages
            .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var msgContent = result.Success
            ? ContentGenerator.GeneratePatchComplete(result.Message, previewRestarted)
            : (result.Message.Contains("ya") || result.Message.Contains("sin cambios")
                ? ContentGenerator.GeneratePatchSkipped(result.Message)
                : ContentGenerator.GeneratePatchFailed(result.Message));

        if (lastMsg is not null) lastMsg.Content = msgContent;
        else AddRuntimeMessage(db, productId, msgContent);

        product.IsProcessing = false;
        product.RuntimePhase = "building";
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);
    }

    // ── Validation + Autofix ──────────────────────────────────────────────────

    private async Task HandleValidation(Guid productId, AppDbContext db, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.FileRevisions.OrderByDescending(r => r.CreatedAt))
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null || string.IsNullOrWhiteSpace(product.ProjectPath)) return;

        product.RuntimeHealth = "recovering";
        product.IsProcessing  = true;
        product.UpdatedAt     = DateTime.UtcNow;

        AddActivity(db, productId, ActivityType.ValidationStarted, "Validación iniciada",
            "Ejecutando quality gates: registry · runtime · build");
        await db.SaveChangesAsync(ct);

        var run = new Domain.Entities.ValidationRun { ProductId = productId };
        db.ValidationRuns.Add(run);
        await db.SaveChangesAsync(ct);

        const int MaxAutofixRounds = 2;
        var autofixLog = new List<string>();

        // ── Round 0: run all gates ─────────────────────────────────────────────
        var (allGates, logs, errors) = await validationEngine.RunAllGatesAsync(product, ct);

        // ── Autofix loop ───────────────────────────────────────────────────────
        for (var round = 0; round < MaxAutofixRounds && allGates.Any(g => !g.Passed && !g.Skipped); round++)
        {
            var failed = allGates.Where(g => !g.Passed && !g.Skipped).ToList();

            run.AutofixAttempts++;
            AddActivity(db, productId, ActivityType.AutofixStarted,
                $"Autofix round {round + 1} — {failed.Count} gate(s) fallidos",
                string.Join(", ", failed.Select(g => g.Gate)));
            await db.SaveChangesAsync(ct);

            var anyFixed = false;
            foreach (var failedGate in failed)
            {
                var (wasFixed, action) = await autofixEngine.TryFixGateAsync(failedGate, product, db, ct);
                autofixLog.Add($"[{(wasFixed ? "FIXED" : "NOOP")}] {failedGate.Gate}: {action}");

                if (wasFixed)
                {
                    anyFixed = true;
                    AddActivity(db, productId, ActivityType.AutofixSucceeded,
                        $"Autofix: {failedGate.Gate}", action);
                }
                else
                {
                    AddActivity(db, productId, ActivityType.AutofixFailed,
                        $"Autofix sin efecto: {failedGate.Gate}", action);
                }
            }

            await db.SaveChangesAsync(ct);

            if (!anyFixed) break;

            // Wait for fixes (e.g., preview restart) before re-checking
            await Task.Delay(4000, ct);

            // Refresh product state from DB before re-run
            product = await db.Products
                .Include(p => p.FileRevisions.OrderByDescending(r => r.CreatedAt))
                .FirstOrDefaultAsync(p => p.Id == productId, ct);
            if (product is null) break;

            var (newGates, newLogs, newErrors) = await validationEngine.RunAllGatesAsync(product, ct);
            allGates = newGates;
            logs    += $"\n--- Re-run after autofix round {round + 1} ---\n" + newLogs;
            if (newErrors != null) errors = (errors ?? "") + "\n" + newErrors;
        }

        // ── Finalize ───────────────────────────────────────────────────────────
        var totalFailed = allGates.Count(g => !g.Passed && !g.Skipped);
        var totalPassed = allGates.Count(g => g.Passed);

        run.GatesPassed  = totalPassed;
        run.GatesFailed  = totalFailed;
        run.GateResults  = System.Text.Json.JsonSerializer.Serialize(allGates);
        run.Logs         = logs.Length > 3000 ? logs[..3000] : logs;
        run.Errors       = !string.IsNullOrEmpty(errors) ? (errors.Length > 2000 ? errors[..2000] : errors) : null;
        run.Status       = totalFailed == 0 ? "passed" : "failed";
        run.FinishedAt   = DateTime.UtcNow;

        product!.RuntimeHealth = totalFailed == 0 ? "healthy" : totalFailed == 1 ? "degraded" : "broken";
        product.IsProcessing   = false;
        product.UpdatedAt      = DateTime.UtcNow;

        var actType = totalFailed == 0 ? ActivityType.ValidationPassed : ActivityType.ValidationFailed;
        AddActivity(db, productId, actType,
            totalFailed == 0 ? $"Validación OK — {totalPassed} gate(s)" : $"Validación fallida — {totalFailed} gate(s)",
            run.AutofixAttempts > 0 ? $"{run.AutofixAttempts} autofix(s) aplicado(s)" : null);

        if (totalFailed == 0 && run.AutofixAttempts > 0)
            AddActivity(db, productId, ActivityType.RuntimeRecovered, "Runtime recuperado",
                $"Autofix resolvió los problemas en {run.AutofixAttempts} ronda(s)");

        var lastMsg = await db.ChatMessages
            .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var msgContent = totalFailed == 0
            ? ContentGenerator.GenerateValidationPassed(totalPassed, run.AutofixAttempts)
            : ContentGenerator.GenerateValidationFailed(totalFailed, run.AutofixAttempts, autofixLog);

        if (lastMsg is not null) lastMsg.Content = msgContent;
        else AddRuntimeMessage(db, productId, msgContent);

        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);

        logger.LogInformation(
            "ValidationRun {RunId}: {Status} | passed={Passed} failed={Failed} autofixes={Fixes}",
            run.Id, run.Status, totalPassed, totalFailed, run.AutofixAttempts);
    }

    // ── Deploy Pipeline ───────────────────────────────────────────────────────

    private async Task HandleDeploy(Guid productId, AppDbContext db, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([productId], ct);
        if (product is null) return;

        // ── Safety checks ─────────────────────────────────────────────────────
        if (product.ScaffoldStatus != "complete" || string.IsNullOrWhiteSpace(product.ProjectPath))
        {
            AddRuntimeMessage(db, productId, "Deploy cancelado: el scaffold debe estar completo antes de poder deployar.");
            await db.SaveChangesAsync(ct);
            return;
        }

        if (product.RuntimeHealth == "broken")
        {
            AddRuntimeMessage(db, productId,
                "Deploy cancelado: el runtime health es **broken**. Ejecutá `valida el proyecto` y resolvé los problemas primero.");
            await db.SaveChangesAsync(ct);
            return;
        }

        if (product.DeployStatus is "preparing" or "building" or "deploying")
        {
            AddRuntimeMessage(db, productId, "Ya hay un deploy en progreso. Esperá a que termine antes de iniciar otro.");
            await db.SaveChangesAsync(ct);
            return;
        }

        // ── Create DeployRun ──────────────────────────────────────────────────
        var (commitHash, branch) = await deployEngine.GetGitInfoAsync(product.ProjectPath, ct);

        var run = new DeployRun
        {
            ProductId  = productId,
            CommitHash = commitHash,
            Branch     = branch,
        };
        db.DeployRuns.Add(run);

        product.DeployStatus = "preparing";
        product.IsProcessing = true;
        product.UpdatedAt    = DateTime.UtcNow;

        AddActivity(db, productId, ActivityType.DeployStarted, "Deploy iniciado",
            $"branch: {branch ?? "unknown"} · commit: {commitHash ?? "unknown"}");
        AddRuntimeMessage(db, productId, ContentGenerator.GenerateDeployStart());
        await db.SaveChangesAsync(ct);

        // ── Pre-deploy gates (includes next_build — runs `npx next build`) ────
        AddActivity(db, productId, ActivityType.DeployBuildStarted, "Pre-deploy gates iniciados",
            "Ejecutando 8 quality gates + next build (puede tomar hasta 5 min)...");
        product.DeployStatus = "building";
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var (gates, allPassed, gateLogs) = await deployEngine.RunPreDeployGatesAsync(product, ct);

        run.GateResults = System.Text.Json.JsonSerializer.Serialize(
            gates.Select(g => new { g.Gate, g.Category, g.Passed, g.Skipped, g.Message, g.Detail }));

        if (!allPassed)
        {
            var failed  = gates.Where(g => !g.Passed && !g.Skipped).Select(g => g.Gate).ToList();
            var errMsg  = $"Gates fallaron: {string.Join(", ", failed)}";

            run.Status     = "failed";
            run.FinishedAt = DateTime.UtcNow;
            run.Logs       = gateLogs.Length > 5000 ? gateLogs[..5000] : gateLogs;
            run.Errors     = errMsg;

            product.DeployStatus = "failed";
            product.IsProcessing = false;
            product.DeployLogs   = gateLogs.Length > 3000 ? gateLogs[..3000] : gateLogs;
            product.UpdatedAt    = DateTime.UtcNow;

            AddActivity(db, productId, ActivityType.DeployFailed, "Deploy fallido — gates no pasaron",
                errMsg.Length > 150 ? errMsg[..150] : errMsg);

            var failMsg = await db.ChatMessages
                .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var gateFailContent = ContentGenerator.GenerateDeployFailed(errMsg);
            if (failMsg is not null) failMsg.Content = gateFailContent;
            else AddRuntimeMessage(db, productId, gateFailContent);

            await db.SaveChangesAsync(ct);
            return;
        }

        AddActivity(db, productId, ActivityType.DeployBuildPassed, "Pre-deploy gates pasaron",
            $"{gates.Count(g => g.Passed)} gates OK · iniciando deploy a proveedor");

        product.DeployStatus = "deploying";
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // ── Execute deploy ────────────────────────────────────────────────────
        var (success, deployUrl, deployLogs) = await deployEngine.ExecuteDeployAsync(product, ct);

        var combinedLogs = gateLogs + "\n\n--- DEPLOY ---\n" + deployLogs;
        run.Logs       = combinedLogs.Length > 5000 ? combinedLogs[..5000] : combinedLogs;
        run.FinishedAt = DateTime.UtcNow;

        product.DeployLogs   = deployLogs.Length > 3000 ? deployLogs[..3000] : deployLogs;
        product.IsProcessing = false;
        product.UpdatedAt    = DateTime.UtcNow;

        var lastMsg = await db.ChatMessages
            .Where(m => m.ProductId == productId && m.Role == MessageRole.Runtime)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (success)
        {
            run.Status    = "passed";
            run.DeployUrl = deployUrl;

            product.DeployStatus           = "deployed";
            product.DeployUrl              = deployUrl;
            product.DeployedAt             = DateTime.UtcNow;
            product.LastSuccessfulDeployAt = DateTime.UtcNow;
            product.DeployCommitHash       = commitHash;
            product.DeployBranch           = branch;

            AddActivity(db, productId, ActivityType.DeploySucceeded, "Deploy exitoso",
                deployUrl ?? "URL no disponible");

            var successContent = ContentGenerator.GenerateDeploySuccess(deployUrl);
            if (lastMsg is not null) lastMsg.Content = successContent;
            else AddRuntimeMessage(db, productId, successContent);
        }
        else
        {
            run.Status = "failed";
            run.Errors = deployLogs.Length > 2000 ? deployLogs[..2000] : deployLogs;

            product.DeployStatus = "failed";

            var reason = deployLogs.Length > 200 ? deployLogs[..200] + "…" : deployLogs;
            AddActivity(db, productId, ActivityType.DeployFailed, "Deploy fallido",
                reason.Length > 150 ? reason[..150] : reason);

            var failedContent = ContentGenerator.GenerateDeployFailed(reason);
            if (lastMsg is not null) lastMsg.Content = failedContent;
            else AddRuntimeMessage(db, productId, failedContent);
        }

        await db.SaveChangesAsync(ct);
        await bus.PingAsync(productId);

        logger.LogInformation(
            "DeployRun {RunId}: {Status} | url={Url} | gates={GateCount}",
            run.Id, run.Status, run.DeployUrl, gates.Count);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<Artifact> UpsertArtifact(AppDbContext db, Guid productId, string type, string title, string content)
    {
        var existing = await db.Artifacts
            .Where(a => a.ProductId == productId && a.Type == type && a.Status != ArtifactStatus.Superseded)
            .ToListAsync();

        var nextVersion = existing.Any() ? existing.Max(a => a.Version) + 1 : 1;

        foreach (var old in existing)
            old.Status = ArtifactStatus.Superseded;

        var artifact = new Artifact
        {
            ProductId   = productId,
            Type        = type,
            Title       = title,
            Content     = content,
            Version     = nextVersion,
            Status      = ArtifactStatus.Draft,
            GeneratedAt = DateTime.UtcNow,
        };
        db.Artifacts.Add(artifact);
        return artifact;
    }

    private static async Task SetProcessing(Product product, ProductStatus status, string phase, AppDbContext db)
    {
        product.Status       = status;
        product.IsProcessing = true;
        product.RuntimePhase = phase;
        product.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static void AddActivity(AppDbContext db, Guid productId, ActivityType type, string title, string? details = null, Guid? artifactId = null)
        => db.ActivityEvents.Add(new ActivityEvent { ProductId = productId, EventType = type, Title = title, Details = details, ArtifactId = artifactId });

    private static void AddRuntimeMessage(AppDbContext db, Guid productId, string content)
        => db.ChatMessages.Add(new ChatMessage { ProductId = productId, Role = MessageRole.Runtime, Content = content });

    private static void SaveMemory(AppDbContext db, Guid productId, string key, string value)
        => db.ProductMemories.Add(new ProductMemory { ProductId = productId, Key = key, Value = value });

    private static async Task<string?> GetMemory(AppDbContext db, Guid productId, string key)
        => (await db.ProductMemories
               .Where(m => m.ProductId == productId && m.Key == key)
               .OrderByDescending(m => m.CreatedAt)
               .FirstOrDefaultAsync())?.Value;

    private static string ExtractFeatureName(string content)
    {
        var s = content.ToLowerInvariant().Trim();

        // Step 1: strip leading verb + article combos (longest first to avoid partial matches)
        string[] verbPrefixes =
        [
            "quiero que agregues ", "quiero que agreguemos ", "necesito que agregues ",
            "por favor agrega ", "por favor agregá ",
            "agregá un ", "agregá una ", "agregá el ", "agregá la ",
            "agrega un ", "agrega una ", "agrega el ", "agrega la ",
            "agregar un ", "agregar una ", "agregar el ", "agregar la ",
            "quiero un ", "quiero una ", "quiero el ", "quiero la ",
            "necesito un ", "necesito una ", "necesito el ", "necesito la ",
            "agregá ", "agrega ", "agregar ", "añadí ", "añade ", "añadir ",
            "quiero ", "necesito ", "implementá ", "implementa ", "implementar ",
        ];
        foreach (var p in verbPrefixes)
            if (s.StartsWith(p)) { s = s[p.Length..]; break; }

        // Step 2: strip trailing context noise (loop to handle chained suffixes)
        string[] tails =
        [
            " al sistema", " a la aplicación", " a la app", " al proyecto",
            " para el sistema", " para la app", " en el sistema", " en la app",
            " que sea robusto", " que sea completo", " completo", " funcional",
            " por favor", " gracias",
        ];
        bool stripped;
        do
        {
            stripped = false;
            foreach (var t in tails)
            {
                if (s.EndsWith(t)) { s = s[..^t.Length]; stripped = true; break; }
            }
        } while (stripped);

        // Step 3: strip leading administrative / management nouns
        // Normalize both sides so accented and unaccented inputs both match
        static string Norm(string t) => t
            .Replace("á","a").Replace("é","e").Replace("í","i").Replace("ó","o").Replace("ú","u")
            .Replace("ñ","n").Replace("ü","u");

        var sNorm = Norm(s);
        string[] adminPrefixes =
        [
            "gestion de ", "modulo de ", "manejo de ", "control de ",
            "administracion de ", "registro de ", "sistema de ",
            "panel de ", "seccion de ", "area de ", "apartado de ",
            "controlar ", "manejar ", "administrar ", "gestionar ", "registrar ",
        ];
        foreach (var a in adminPrefixes)
            if (sNorm.StartsWith(a)) { s = s[a.Length..]; break; }

        s = s.TrimEnd('.', '!', '?', ',', ';').Trim();

        // Step 4: if still 4+ words, extract the first meaningful noun (≥4 chars, non-stopword)
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 4)
        {
            string[] stopwords = ["para", "con", "del", "los", "las", "sus", "que", "por", "una", "los"];
            var core = words.FirstOrDefault(w => w.Length >= 4 && !stopwords.Contains(w)) ?? words[0];
            s = core;
        }

        // Step 5: title-case
        return string.Join(" ", s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w));
    }

    private static string ExtractWidgetName(string content)
    {
        var lower = content.ToLowerInvariant().Trim();

        // Strip leading verb phrases
        string[] prefixes = ["agrega ", "agregá ", "añade ", "incluye ", "quiero ", "necesito "];
        foreach (var prefix in prefixes)
            if (lower.StartsWith(prefix)) { lower = lower[prefix.Length..]; break; }

        // Strip dashboard context suffixes
        string[] suffixes = [" al dashboard", " en el dashboard", " en dashboard", " al dash", " para el dashboard", " para dashboard"];
        foreach (var suffix in suffixes)
            if (lower.EndsWith(suffix)) { lower = lower[..^suffix.Length]; break; }

        // Strip common modifiers
        lower = lower.Replace("un widget de ", "").Replace("una sección de ", "").Replace("un módulo de ", "")
                     .Replace("un widget ", "").Replace("nueva métrica de ", "").Replace("nuevas métricas de ", "")
                     .TrimEnd('.', '!', '?', ',', ';').Trim();

        return string.Join(" ", lower.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w));
    }

    private static string ToDeltaRoute(string featureName)
    {
        // featureName is already clean (e.g. "Proveedores", "Facturación")
        // Normalize accents then take first meaningful word
        var lower = featureName.ToLowerInvariant()
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
            .Replace("ñ", "n").Replace("ü", "u");
        var first = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(w => w.Length >= 3) ?? lower;
        return System.Text.RegularExpressions.Regex.Replace(first, @"[^a-z0-9-]", "");
    }

    private static string BuildFeatureArtifactContent(string featureName, string productName,
        IReadOnlyList<ScaffoldChange> changes)
    {
        var created = changes.Where(c => c.ChangeType == "created").ToList();
        var skipped = changes.Where(c => c.ChangeType == "skipped").ToList();

        var createdLines = created.Count > 0
            ? string.Join("\n", created.Select(c => "- `" + Path.GetFileName(c.TargetPath) + "` (" + c.Layer + ")"))
            : "_Ninguno_";
        var skippedLines = skipped.Count > 0
            ? string.Join("\n", skipped.Select(c => "- `" + Path.GetFileName(c.TargetPath) + "` (ya existía)"))
            : "_Ninguno_";

        return $"""
            ## Módulo: {featureName}

            **Producto**: {productName}
            **Generado**: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC
            **Estado**: {(created.Count > 0 ? "Generado" : "Ya existía — sin cambios")}

            ### Archivos creados ({created.Count})
            {createdLines}

            ### Archivos saltados ({skipped.Count})
            {skippedLines}

            ### Próximos pasos
            - Registrar `DbSet<{featureName.Replace(" ", "")}> ...` en AppDbContext
            - Agregar la ruta en `components/Sidebar.tsx`
            - Agregar migración EF Core para la entidad nueva
            """;
    }
}
