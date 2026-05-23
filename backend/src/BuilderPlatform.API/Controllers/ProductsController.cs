using BuilderPlatform.API.DTOs;
using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using BuilderPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Threading.Channels;

namespace BuilderPlatform.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController(AppDbContext db, RuntimeOrchestrator orchestrator, PreviewRunner previewRunner, RuntimeEventBus eventBus) : ControllerBase
{
    // ── Ownership helpers ─────────────────────────────────────────────────────

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<Product?> FindOwned(Guid id) =>
        await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == CurrentUserId);

    private async Task<bool> OwnsProduct(Guid id) =>
        await db.Products.AnyAsync(p => p.Id == id && p.OwnerUserId == CurrentUserId);

    // GET /api/products
    [HttpGet]
    public async Task<IEnumerable<ProductSummaryDto>> GetAll()
    {
        var uid = CurrentUserId;
        return await db.Products
            .Where(p => p.OwnerUserId == uid)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new ProductSummaryDto(
                p.Id, p.Name, p.Status.ToString(), p.PreviewUrl, p.PreviewStatus, p.PreviewPort,
                p.IsProcessing, p.RuntimePhase, p.ProjectPath, p.ScaffoldStatus, p.RuntimeHealth,
                p.CreatedAt, p.UpdatedAt,
                p.DeployStatus, p.DeployUrl, p.DeployedAt,
                p.Memory.Where(m => m.Key == "industry").OrderByDescending(m => m.CreatedAt).Select(m => m.Value).FirstOrDefault()
            ))
            .ToListAsync();
    }

    // GET /api/products/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetailDto>> GetById(Guid id)
    {
        var p = await db.Products
            .Include(p => p.Messages.OrderBy(m => m.CreatedAt))
            .Include(p => p.ActivityEvents.OrderByDescending(a => a.CreatedAt))
            .Include(p => p.Approvals.OrderByDescending(a => a.CreatedAt))
            .Include(p => p.Memory.OrderByDescending(m => m.CreatedAt))
            .Include(p => p.Artifacts.OrderByDescending(a => a.GeneratedAt))
            .Include(p => p.ScaffoldEntries.OrderBy(s => s.SortOrder))
            .Include(p => p.ScaffoldChanges.OrderByDescending(s => s.CreatedAt))
            .Include(p => p.Modules.OrderBy(m => m.DetectedAt))
            .Include(p => p.FileRevisions.OrderByDescending(r => r.CreatedAt))
            .Include(p => p.ValidationRuns.OrderByDescending(r => r.StartedAt))
            .Include(p => p.DeployRuns.OrderByDescending(r => r.StartedAt))
            .Include(p => p.RefactorRecommendations.OrderByDescending(r => r.CreatedAt))
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == CurrentUserId);

        if (p is null) return NotFound();

        return ToDetailDto(p);
    }

    // POST /api/products
    [HttpPost]
    public async Task<ActionResult<ProductSummaryDto>> Create(CreateProductRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Prompt))
            return BadRequest(new { error = "Name and Prompt are required" });

        var product = new Product
        {
            Name         = req.Name.Trim(),
            Prompt       = req.Prompt.Trim(),
            Status       = ProductStatus.Draft,
            IsProcessing = false,
            RuntimePhase = "queued",
            OwnerUserId  = CurrentUserId,
        };

        // First user message stored in chat history
        db.ChatMessages.Add(new ChatMessage
        {
            ProductId      = product.Id,
            Role           = MessageRole.User,
            Content        = req.Prompt.Trim(),
            DetectedIntent = "create_product",
            Confidence     = 1.0,
        });

        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Trigger runtime pipeline asynchronously
        orchestrator.Enqueue(new RuntimeWork("init", product.Id));

        var dto = new ProductSummaryDto(
            product.Id, product.Name, product.Status.ToString(), product.PreviewUrl,
            product.PreviewStatus, product.PreviewPort,
            product.IsProcessing, product.RuntimePhase, product.ProjectPath, product.ScaffoldStatus,
            product.RuntimeHealth, product.CreatedAt, product.UpdatedAt,
            product.DeployStatus, product.DeployUrl, product.DeployedAt, null);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, dto);
    }

    // PATCH /api/products/{id}/status
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ProductSummaryDto>> UpdateStatus(Guid id, UpdateProductStatusRequest req)
    {
        var product = await FindOwned(id);
        if (product is null) return NotFound();

        if (!Enum.TryParse<ProductStatus>(req.Status, true, out var newStatus))
            return BadRequest(new { error = $"Invalid status: {req.Status}" });

        var old = product.Status;
        product.Status    = newStatus;
        product.UpdatedAt = DateTime.UtcNow;

        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId = product.Id,
            EventType = ActivityType.StatusChanged,
            Title     = $"Estado cambiado: {old} → {newStatus}",
        });

        await db.SaveChangesAsync();
        return ToSummaryDto(product);
    }

    // PATCH /api/products/{id}/preview-url
    [HttpPatch("{id:guid}/preview-url")]
    public async Task<ActionResult<ProductSummaryDto>> UpdatePreviewUrl(Guid id, UpdatePreviewUrlRequest req)
    {
        var product = await FindOwned(id);
        if (product is null) return NotFound();

        product.PreviewUrl = req.PreviewUrl;
        product.UpdatedAt  = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ToSummaryDto(product);
    }

    // DELETE /api/products/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await FindOwned(id);
        if (product is null) return NotFound();

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/products/{id}/messages
    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(Guid id, SendMessageRequest req)
    {
        var product = await FindOwned(id);
        if (product is null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { error = "Content is required" });

        var content = req.Content.Trim();

        var userMsg = new ChatMessage
        {
            ProductId = product.Id,
            Role      = MessageRole.User,
            Content   = content,
        };

        // Placeholder runtime response — will be overwritten by orchestrator with intent-aware content
        var runtimeMsg = new ChatMessage
        {
            ProductId = product.Id,
            Role      = MessageRole.Runtime,
            Content   = "Procesando tu mensaje...",
            CreatedAt = DateTime.UtcNow.AddMilliseconds(500),
        };

        db.ChatMessages.AddRange(userMsg, runtimeMsg);
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Enqueue intent classification + smart response generation
        orchestrator.Enqueue(new RuntimeWork("message", product.Id, content));

        return CreatedAtAction(nameof(GetById), new { id },
            new MessageDto(userMsg.Id, userMsg.Role.ToString(), userMsg.Content,
                userMsg.DetectedIntent, userMsg.Confidence, userMsg.CreatedAt));
    }

    // POST /api/products/{id}/approvals/{approvalId}/resolve
    [HttpPost("{id:guid}/approvals/{approvalId:guid}/resolve")]
    public async Task<ActionResult<ApprovalDto>> ResolveApproval(Guid id, Guid approvalId, ResolveApprovalRequest req)
    {
        if (!await OwnsProduct(id)) return NotFound();

        var approval = await db.Approvals.FirstOrDefaultAsync(a => a.Id == approvalId && a.ProductId == id);
        if (approval is null) return NotFound();

        if (approval.Status != ApprovalStatus.Pending)
            return BadRequest(new { error = "Approval is already resolved" });

        approval.Status         = req.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        approval.ResolutionNote = req.Note;
        approval.ResolvedAt     = DateTime.UtcNow;

        var product = await db.Products.FindAsync(id);
        if (product is not null) product.UpdatedAt = DateTime.UtcNow;

        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId = id,
            EventType = ActivityType.ApprovalResolved,
            Title     = req.Approved ? $"Aprobado: {approval.Title}" : $"Rechazado: {approval.Title}",
            Details   = req.Note,
        });

        await db.SaveChangesAsync();

        // Trigger orchestrator to advance lifecycle
        orchestrator.Enqueue(new RuntimeWork("approval_resolved", id, req.Approved.ToString()));

        return new ApprovalDto(
            approval.Id, approval.Title, approval.Description,
            approval.Status.ToString(), approval.ResolutionNote,
            approval.ArtifactId, approval.CreatedAt, approval.ResolvedAt);
    }

    // POST /api/products/{id}/preview/start
    [HttpPost("{id:guid}/preview/start")]
    public async Task<IActionResult> StartPreview(Guid id)
    {
        var product = await FindOwned(id);
        if (product is null) return NotFound();

        if (string.IsNullOrEmpty(product.ProjectPath) || product.ScaffoldStatus != "complete")
            return BadRequest(new { error = "Scaffold debe estar completo antes de iniciar el preview" });

        if (product.PreviewStatus == "starting")
            return BadRequest(new { error = "Preview ya está iniciando" });

        previewRunner.StartAsync(id);
        return Accepted(new { message = "Preview iniciando — monitoreá el estado con GET /products/{id}" });
    }

    // POST /api/products/{id}/preview/stop
    [HttpPost("{id:guid}/preview/stop")]
    public async Task<IActionResult> StopPreview(Guid id)
    {
        var product = await FindOwned(id);
        if (product is null) return NotFound();

        await previewRunner.StopAsync(id);
        return Ok(new { message = "Preview detenido" });
    }

    // POST /api/products/{id}/open-vscode
    [HttpPost("{id:guid}/open-vscode")]
    public async Task<IActionResult> OpenInVSCode(Guid id, [FromServices] IConfiguration config)
    {
        var product = await FindOwned(id);
        if (product is null) return NotFound();

        if (string.IsNullOrWhiteSpace(product.ProjectPath))
            return BadRequest(new { error = "Este producto no tiene un proyecto generado. Generá el scaffold primero." });

        // Safety: only allow paths inside the configured scaffold output directory
        var outputPath = config["Scaffold:OutputPath"];
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var safeBase   = Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var targetPath = Path.GetFullPath(product.ProjectPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!targetPath.StartsWith(safeBase, StringComparison.OrdinalIgnoreCase))
            {
                db.ActivityEvents.Add(new ActivityEvent
                {
                    ProductId = id,
                    EventType = ActivityType.VSCodeOpenFailed,
                    Title     = "Ruta rechazada por seguridad",
                    Details   = product.ProjectPath,
                });
                await db.SaveChangesAsync();
                return BadRequest(new { error = "Ruta de proyecto fuera del directorio permitido." });
            }
        }

        if (!Directory.Exists(product.ProjectPath))
        {
            db.ActivityEvents.Add(new ActivityEvent
            {
                ProductId = id,
                EventType = ActivityType.VSCodeOpenFailed,
                Title     = "Directorio del proyecto no encontrado",
                Details   = product.ProjectPath,
            });
            await db.SaveChangesAsync();
            return BadRequest(new { error = $"Directorio del proyecto no encontrado: {product.ProjectPath}" });
        }

        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId = id,
            EventType = ActivityType.VSCodeOpenRequested,
            Title     = "Abriendo en VS Code",
            Details   = product.ProjectPath,
        });
        await db.SaveChangesAsync();

        // Check if 'code' is available in PATH
        using var whereProc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName               = "cmd.exe",
            Arguments              = "/c where code",
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        })!;
        using var whereCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await whereProc.WaitForExitAsync(whereCts.Token);

        if (whereProc.ExitCode != 0)
        {
            db.ActivityEvents.Add(new ActivityEvent
            {
                ProductId = id,
                EventType = ActivityType.VSCodeOpenFailed,
                Title     = "VS Code CLI no encontrado",
                Details   = "El comando 'code' no está disponible en el PATH.",
            });
            await db.SaveChangesAsync();
            return UnprocessableEntity(new
            {
                error   = "VS Code no instalado o el comando 'code' no está en el PATH.",
                details = "Instalá VS Code y luego: View → Command Palette → 'Shell Command: Install code command in PATH'",
            });
        }

        // Launch VS Code — fire and don't wait (VS Code stays open)
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "cmd.exe",
            Arguments       = $"/c code \"{product.ProjectPath}\"",
            UseShellExecute = false,
            CreateNoWindow  = true,
        });

        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId = id,
            EventType = ActivityType.VSCodeOpenSucceeded,
            Title     = "VS Code abierto",
            Details   = product.ProjectPath,
        });
        await db.SaveChangesAsync();

        return Ok(new { message = "VS Code abriendo el proyecto...", path = product.ProjectPath });
    }

    // GET /api/products/{id}/runtime-files
    [HttpGet("{id:guid}/runtime-files")]
    public async Task<ActionResult<IEnumerable<ManagedFileDto>>> GetRuntimeFiles(
        Guid id, [FromServices] RuntimePatchEngine patchEngine)
    {
        var product = await db.Products
            .Include(p => p.FileRevisions)
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == CurrentUserId);

        if (product is null) return NotFound();
        if (string.IsNullOrWhiteSpace(product.ProjectPath))
            return Ok(Array.Empty<ManagedFileDto>());

        var files = patchEngine.GetManagedFiles(product.ProjectPath, product.FileRevisions.ToList());
        return Ok(files.Select(f => new ManagedFileDto(
            f.RelativePath, f.DisplayName, f.FileType,
            f.Exists, f.LastModified, f.IsEditable, f.RevisionCount)));
    }

    // GET /api/products/{id}/revisions/{revisionId}
    [HttpGet("{id:guid}/revisions/{revisionId:guid}")]
    public async Task<ActionResult> GetRevision(Guid id, Guid revisionId)
    {
        if (!await OwnsProduct(id)) return NotFound();

        var revision = await db.FileRevisions
            .FirstOrDefaultAsync(r => r.Id == revisionId && r.ProductId == id);

        if (revision is null) return NotFound();

        return Ok(new
        {
            id            = revision.Id,
            relativePath  = revision.RelativePath,
            patchType     = revision.PatchType,
            reason        = revision.Reason,
            beforeContent = revision.BeforeContent,
            afterContent  = revision.AfterContent,
            createdAt     = revision.CreatedAt,
        });
    }

    // POST /api/products/{id}/validate
    [HttpPost("{id:guid}/validate")]
    public async Task<IActionResult> TriggerValidation(Guid id)
    {
        var product = await FindOwned(id);
        if (product is null) return NotFound();

        if (string.IsNullOrEmpty(product.ProjectPath) || product.ScaffoldStatus != "complete")
            return BadRequest(new { error = "Scaffold debe estar completo para ejecutar validación" });

        orchestrator.Enqueue(new RuntimeWork("validation", id));
        return Accepted(new { message = "Validación iniciada — seguí el progreso en el tab Calidad" });
    }

    // GET /api/products/{id}/validations
    [HttpGet("{id:guid}/validations")]
    public async Task<ActionResult<IEnumerable<ValidationRunSummaryDto>>> GetValidations(Guid id)
    {
        if (!await OwnsProduct(id)) return NotFound();

        var runs = await db.ValidationRuns
            .Where(r => r.ProductId == id)
            .OrderByDescending(r => r.StartedAt)
            .Take(10)
            .Select(r => new ValidationRunSummaryDto(r.Id, r.Status, r.StartedAt, r.FinishedAt,
                r.AutofixAttempts, r.GatesPassed, r.GatesFailed))
            .ToListAsync();

        return Ok(runs);
    }

    // GET /api/products/{id}/validations/{runId}
    [HttpGet("{id:guid}/validations/{runId:guid}")]
    public async Task<ActionResult<ValidationRunDetailDto>> GetValidationRun(Guid id, Guid runId)
    {
        if (!await OwnsProduct(id)) return NotFound();

        var run = await db.ValidationRuns
            .FirstOrDefaultAsync(r => r.Id == runId && r.ProductId == id);

        if (run is null) return NotFound();

        var gates = System.Text.Json.JsonSerializer.Deserialize<List<GateResultDto>>(run.GateResults ?? "[]") ?? [];

        return Ok(new ValidationRunDetailDto(
            run.Id, run.Status, run.StartedAt, run.FinishedAt,
            run.Logs, run.Errors, run.AutofixAttempts, run.GatesPassed, run.GatesFailed, gates));
    }

    // POST /api/products/{id}/deploy
    [HttpPost("{id:guid}/deploy")]
    public async Task<IActionResult> TriggerDeploy(Guid id)
    {
        var product = await FindOwned(id);
        if (product is null) return NotFound();

        if (product.ScaffoldStatus != "complete" || string.IsNullOrWhiteSpace(product.ProjectPath))
            return BadRequest(new { error = "El scaffold debe estar completo antes de poder deployar" });

        if (product.RuntimeHealth == "broken")
            return BadRequest(new { error = "No se puede deployar con runtimeHealth = broken. Ejecutá validación primero." });

        if (product.DeployStatus is "preparing" or "building" or "deploying")
            return BadRequest(new { error = "Ya hay un deploy en progreso" });

        orchestrator.Enqueue(new RuntimeWork("deploy", id));
        return Accepted(new { message = "Deploy iniciado — seguí el progreso en el tab Deploy" });
    }

    // GET /api/products/{id}/deployments
    [HttpGet("{id:guid}/deployments")]
    public async Task<ActionResult<IEnumerable<DeployRunSummaryDto>>> GetDeployments(Guid id)
    {
        if (!await OwnsProduct(id)) return NotFound();

        var runs = await db.DeployRuns
            .Where(r => r.ProductId == id)
            .OrderByDescending(r => r.StartedAt)
            .Take(10)
            .Select(r => new DeployRunSummaryDto(
                r.Id, r.Status, r.StartedAt, r.FinishedAt, r.DeployUrl, r.CommitHash, r.Branch))
            .ToListAsync();

        return Ok(runs);
    }

    // GET /api/products/{id}/deployments/{runId}
    [HttpGet("{id:guid}/deployments/{runId:guid}")]
    public async Task<ActionResult<DeployRunDetailDto>> GetDeploymentRun(Guid id, Guid runId)
    {
        if (!await OwnsProduct(id)) return NotFound();

        var run = await db.DeployRuns
            .FirstOrDefaultAsync(r => r.Id == runId && r.ProductId == id);

        if (run is null) return NotFound();

        var gates = System.Text.Json.JsonSerializer.Deserialize<List<GateResultDto>>(run.GateResults ?? "[]") ?? [];

        return Ok(new DeployRunDetailDto(
            run.Id, run.Status, run.StartedAt, run.FinishedAt,
            run.Logs, run.Errors, run.DeployUrl, run.CommitHash, run.Branch, gates));
    }

    // GET /api/products/{id}/refactor
    [HttpGet("{id:guid}/refactor")]
    public async Task<ActionResult<IEnumerable<RefactorRecommendationDto>>> GetRefactorRecommendations(Guid id)
    {
        if (!await OwnsProduct(id)) return NotFound();
        var recs = await db.RefactorRecommendations
            .Where(r => r.ProductId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        return Ok(recs.Select(r => new RefactorRecommendationDto(
            r.Id, r.Type, r.Title, r.Severity, r.Reason, r.Impact, r.Risk,
            r.Status, r.Note, r.ArtifactId, r.CreatedAt, r.ResolvedAt,
            r.ExecutedAt, r.ExecutionError)));
    }

    // POST /api/products/{id}/refactor/{recId}/resolve
    [HttpPost("{id:guid}/refactor/{recId:guid}/resolve")]
    public async Task<ActionResult<RefactorRecommendationDto>> ResolveRefactor(
        Guid id, Guid recId, ResolveRefactorRequest req,
        [FromServices] ProductEvolutionService evolutionService)
    {
        if (!await OwnsProduct(id)) return NotFound();

        var rec = await db.RefactorRecommendations
            .FirstOrDefaultAsync(r => r.Id == recId && r.ProductId == id);
        if (rec is null) return NotFound();
        if (rec.Status != "pending") return BadRequest(new { error = "Recommendation already resolved" });

        rec.Status     = req.Accepted ? "accepted" : "rejected";
        rec.Note       = req.Note;
        rec.ResolvedAt = DateTime.UtcNow;

        var product = await db.Products.FindAsync(id);
        if (product is not null) product.UpdatedAt = DateTime.UtcNow;

        if (req.Accepted && product is not null)
        {
            // Create Refactor Plan artifact
            var plan = BuildRefactorPlanContent(rec, product.Name);
            var artifact = new Artifact
            {
                ProductId   = id,
                Type        = "refactor_plan",
                Title       = $"Refactor Plan: {rec.Title}",
                Content     = plan,
                Version     = 1,
                Status      = ArtifactStatus.Draft,
            };
            db.Artifacts.Add(artifact);
            rec.ArtifactId = artifact.Id;

            db.ActivityEvents.Add(new ActivityEvent
            {
                ProductId = id,
                EventType = ActivityType.RefactorAccepted,
                Title     = $"Refactor aceptado: {rec.Title}",
                Details   = $"Plan generado — tipo: {rec.Type} · severidad: {rec.Severity}",
                ArtifactId = artifact.Id,
            });

            // Record architectural decision in evolution memory
            var ctx = await evolutionService.GetEvolutionContextAsync(id, db);
            ctx.Decisions.Add(new EvolutionDecision(
                $"Refactor aceptado: {rec.Title} (tipo: {rec.Type})",
                DateTime.UtcNow));
            evolutionService.PersistEvolutionContext(id, ctx, db);
        }
        else
        {
            db.ActivityEvents.Add(new ActivityEvent
            {
                ProductId = id,
                EventType = ActivityType.RefactorRejected,
                Title     = $"Refactor rechazado: {rec.Title}",
                Details   = req.Note,
            });
        }

        await db.SaveChangesAsync();
        return Ok(new RefactorRecommendationDto(
            rec.Id, rec.Type, rec.Title, rec.Severity, rec.Reason, rec.Impact, rec.Risk,
            rec.Status, rec.Note, rec.ArtifactId, rec.CreatedAt, rec.ResolvedAt,
            rec.ExecutedAt, rec.ExecutionError));
    }

    // POST /api/products/{id}/refactor/{recId}/execute
    [HttpPost("{id:guid}/refactor/{recId:guid}/execute")]
    public async Task<ActionResult<RefactorRecommendationDto>> ExecuteRefactor(
        Guid id, Guid recId,
        [FromServices] RefactorExecutionService executionService,
        [FromServices] ProductEvolutionService  evolutionService,
        [FromServices] RuntimeEventBus          bus)
    {
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == CurrentUserId);
        if (product is null) return NotFound();

        var rec = await db.RefactorRecommendations
            .FirstOrDefaultAsync(r => r.Id == recId && r.ProductId == id);
        if (rec is null) return NotFound();
        if (rec.Status != "accepted")
            return BadRequest(new { error = "Solo se pueden ejecutar recomendaciones en estado 'accepted'." });
        if (!executionService.CanExecuteSafely(rec.Type))
            return UnprocessableEntity(new { error = $"El refactor '{rec.Type}' no puede ejecutarse automáticamente. Consulta el Refactor Plan artifact." });
        if (string.IsNullOrWhiteSpace(product.ProjectPath))
            return UnprocessableEntity(new { error = "El producto no tiene proyecto scaffolded." });

        // ── Log start ────────────────────────────────────────────────────────
        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId = id,
            EventType = ActivityType.RefactorExecutionStarted,
            Title     = $"Aplicando refactor: {rec.Title}",
            Details   = $"tipo: {rec.Type} · severidad: {rec.Severity}",
        });

        // ── Execute file changes ─────────────────────────────────────────────
        var result = await executionService.ExecuteFileChangesAsync(product.ProjectPath, rec);

        if (!result.Success)
        {
            rec.Status         = "failed";
            rec.ExecutionError = result.Error;
            product.UpdatedAt  = DateTime.UtcNow;
            db.ActivityEvents.Add(new ActivityEvent
            {
                ProductId = id,
                EventType = ActivityType.RefactorExecutionFailed,
                Title     = $"Refactor fallido: {rec.Title}",
                Details   = result.Error,
            });
            await db.SaveChangesAsync();
            await bus.PingAsync(id);
            return UnprocessableEntity(new { error = result.Error });
        }

        // ── Validate registries ──────────────────────────────────────────────
        var validationOk = await executionService.ValidateRegistriesAsync(product.ProjectPath);
        if (!validationOk)
        {
            db.ActivityEvents.Add(new ActivityEvent
            {
                ProductId = id,
                EventType = ActivityType.RefactorRollbackStarted,
                Title     = $"Rollback iniciado: {rec.Title}",
                Details   = "Registry inválido post-refactor",
            });
            await executionService.RollbackAsync(result.Backups);
            db.ActivityEvents.Add(new ActivityEvent
            {
                ProductId = id,
                EventType = ActivityType.RefactorRollbackCompleted,
                Title     = $"Rollback completado: {rec.Title}",
                Details   = "Archivos restaurados al estado anterior",
            });
            rec.Status         = "failed";
            rec.ExecutionError = "Validación fallida post-refactor. Cambios revertidos automáticamente.";
            product.UpdatedAt  = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await bus.PingAsync(id);
            return UnprocessableEntity(new { error = rec.ExecutionError });
        }

        // ── Success — persist FileRevisions + evolution memory ───────────────
        foreach (var b in result.Backups)
        {
            db.FileRevisions.Add(new FileRevision
            {
                ProductId     = id,
                RelativePath  = b.RelPath,
                PatchType     = $"refactor_execution:{rec.Type}",
                Reason        = $"Refactor aplicado: {rec.Title}",
                BeforeContent = b.Before.Length <= 8000 ? b.Before : b.Before[..8000],
                AfterContent  = b.After.Length  <= 8000 ? b.After  : b.After[..8000],
            });
            db.ActivityEvents.Add(new ActivityEvent
            {
                ProductId = id,
                EventType = ActivityType.RefactorFileUpdated,
                Title     = $"Archivo actualizado: {b.RelPath}",
                Details   = $"Refactor: {rec.Title}",
            });
        }

        // Update evolution memory based on refactor type
        var ctx = await evolutionService.GetEvolutionContextAsync(id, db);
        ApplyEvolutionMemoryUpdate(rec, ctx);
        evolutionService.PersistEvolutionContext(id, ctx, db);

        // Mark recommendation as applied
        rec.Status     = "applied";
        rec.ExecutedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;

        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId = id,
            EventType = ActivityType.RefactorValidationPassed,
            Title     = "Validación post-refactor: OK",
            Details   = $"Registry válido — {result.Backups.Count} archivo(s) actualizado(s)",
        });
        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId  = id,
            EventType  = ActivityType.RefactorExecutionSucceeded,
            Title      = $"Refactor aplicado: {rec.Title}",
            Details    = result.Backups.Count > 0
                ? $"Archivos: {string.Join(", ", result.Backups.Select(b => b.RelPath))}"
                : "Actualización de evolution memory — sin cambios en archivos.",
            ArtifactId = rec.ArtifactId,
        });

        await db.SaveChangesAsync();
        await bus.PingAsync(id);

        return Ok(new RefactorRecommendationDto(
            rec.Id, rec.Type, rec.Title, rec.Severity, rec.Reason, rec.Impact, rec.Risk,
            rec.Status, rec.Note, rec.ArtifactId, rec.CreatedAt, rec.ResolvedAt,
            rec.ExecutedAt, rec.ExecutionError));
    }

    // Applies in-memory evolution context changes based on the refactor type.
    private static void ApplyEvolutionMemoryUpdate(
        Domain.Entities.RefactorRecommendation rec, EvolutionContext ctx)
    {
        switch (rec.Type)
        {
            case "redundant_name":
            {
                var m = System.Text.RegularExpressions.Regex.Match(rec.Title, @"Renombrar '(.+?)' a '(.+?)'");
                if (!m.Success) return;
                var (from, to) = (m.Groups[1].Value, m.Groups[2].Value);
                var mod = ctx.Modules.FirstOrDefault(x =>
                    string.Equals(x.Name, from, StringComparison.OrdinalIgnoreCase));
                if (mod != null)
                    ctx.Modules[ctx.Modules.IndexOf(mod)] = mod with { Name = to };
                ctx.Decisions.Add(new EvolutionDecision(
                    $"Módulo '{from}' renombrado a '{to}' vía refactor execution (Sprint 27)",
                    DateTime.UtcNow));
                break;
            }
            case "duplicate_module":
            {
                var m = System.Text.RegularExpressions.Regex.Match(rec.Title, @"Consolidar '(.+?)' y '(.+?)'");
                if (!m.Success) return;
                ctx.Decisions.Add(new EvolutionDecision(
                    $"Módulo '{m.Groups[1].Value}' marcado para fusión con '{m.Groups[2].Value}' — etiqueta nav actualizada",
                    DateTime.UtcNow));
                break;
            }
            case "orphaned_history":
            {
                var m = System.Text.RegularExpressions.Regex.Match(rec.Title, @"Eliminar '(.+?)' del historial");
                if (m.Success)
                {
                    ctx.FeatureHistory.RemoveAll(h =>
                        string.Equals(h, m.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
                    ctx.Decisions.Add(new EvolutionDecision(
                        $"'{m.Groups[1].Value}' eliminado del historial — sin módulo activo correspondiente",
                        DateTime.UtcNow));
                }
                break;
            }
            case "missing_connection":
            {
                var m = System.Text.RegularExpressions.Regex.Match(rec.Title, @"Conectar '(.+?)' con '(.+?)'");
                if (m.Success)
                {
                    ctx.Relations.Add(new EvolutionRelation(
                        m.Groups[1].Value, m.Groups[2].Value,
                        "conectado_manualmente",
                        "Conexión añadida manualmente vía refactor execution",
                        DateTime.UtcNow));
                    ctx.Decisions.Add(new EvolutionDecision(
                        $"Conexión añadida: '{m.Groups[1].Value}' → '{m.Groups[2].Value}'",
                        DateTime.UtcNow));
                }
                break;
            }
            case "contradictory_relation":
            {
                var m = System.Text.RegularExpressions.Regex.Match(rec.Title, @"'(.+?)'.+?'(.+?)'");
                if (m.Success)
                {
                    var (a, b) = (m.Groups[1].Value, m.Groups[2].Value);
                    var toRemove = ctx.Relations.FirstOrDefault(r =>
                        string.Equals(r.From, b, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r.To,   a, StringComparison.OrdinalIgnoreCase));
                    if (toRemove != null) ctx.Relations.Remove(toRemove);
                    ctx.Decisions.Add(new EvolutionDecision(
                        $"Relación contradictoria resuelta entre '{a}' y '{b}'",
                        DateTime.UtcNow));
                }
                break;
            }
        }
    }

    private static string BuildRefactorPlanContent(
        Domain.Entities.RefactorRecommendation rec, string productName) =>
        $"""
        # Refactor Plan: {rec.Title}

        **Producto**: {productName}
        **Tipo**: {rec.Type}
        **Severidad**: {rec.Severity}
        **Fecha**: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC

        ## Por qué

        {rec.Reason}

        ## Impacto esperado

        {rec.Impact}

        ## Nivel de riesgo

        {rec.Risk}

        ## Pasos sugeridos

        1. Revisar los módulos involucrados en el workspace.
        2. Aplicar el cambio en el sprint siguiente (sin destruir lo existente).
        3. Actualizar el registry y la evolution memory tras el cambio.
        4. Re-validar con quality gates antes del próximo deploy.

        ---
        *Generado automáticamente por el Architectural Refactoring Intelligence Engine — Builder Platform Sprint 26.*
        """;

    // GET /api/products/{id}/evolution
    [HttpGet("{id:guid}/evolution")]
    public async Task<ActionResult<EvolutionContextDto>> GetEvolution(
        Guid id, [FromServices] ProductEvolutionService evolutionService)
    {
        if (!await OwnsProduct(id)) return NotFound();
        var ctx = await evolutionService.GetEvolutionContextAsync(id, db);
        return Ok(new EvolutionContextDto(
            ctx.Modules.Select(m => new EvolutionModuleDto(m.Name, m.Route, m.Layer, m.AddedAt)).ToList(),
            ctx.Relations.Select(r => new EvolutionRelationDto(r.From, r.To, r.RelationType, r.Reason, r.DetectedAt)).ToList(),
            ctx.Decisions.Select(d => new EvolutionDecisionDto(d.Summary, d.MadeAt)).ToList(),
            ctx.FeatureHistory
        ));
    }

    // GET /api/products/{id}/intelligence
    [HttpGet("{id:guid}/intelligence")]
    public async Task<ActionResult<IntelligenceReportDto>> GetIntelligence(
        Guid id, [FromServices] ProductIntelligenceEngine intelligenceEngine, CancellationToken ct)
    {
        if (!await OwnsProduct(id)) return NotFound();
        var report = await intelligenceEngine.AnalyzeAsync(id, db, ct);
        return Ok(new IntelligenceReportDto(
            report.ProductId, report.Industry, report.IndustryLabel, report.ModuleCount,
            report.EvolutionStage, report.EvolutionStageLabel, report.EvolutionNextMilestone,
            report.Gaps.Select(g => new IntelligenceGapDto(g.Module, g.Reason, g.Priority, g.Category)).ToList(),
            report.Connections.Select(c => new IntelligenceConnectionDto(c.From, c.To, c.Label, c.Detected, c.Impact)).ToList(),
            report.Suggestions.Select(s => new IntelligenceSuggestionDto(s.Title, s.Context, s.Impact, s.Category)).ToList(),
            report.Narrative, report.AnalyzedAt,
            report.HealthScore, report.HealthScoreLabel, report.HealthScoreNumeric,
            report.CriticalCount,
            report.TopInsights.Select(i => new ProactiveInsightDto(i.Type, i.Severity, i.Title, i.Detail, i.Action, i.DaysSinceDetectable, i.InsightStage)).ToList(),
            // Sprint 40
            report.ProductAgeDays, report.GapAgeDays, report.OperationalDebtCount,
            report.RecentModuleCount, report.PendingRefactorCount
        ));
    }

    // GET /api/products/{id}/events  — SSE stream (anonymous: EventSource can't send headers)
    [HttpGet("{id:guid}/events")]
    [AllowAnonymous]
    public async Task StreamEvents(Guid id, CancellationToken ct)
    {
        var exists = await db.Products.AnyAsync(p => p.Id == id, ct);
        if (!exists) { Response.StatusCode = 404; return; }

        Response.Headers.ContentType   = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl  = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers["Connection"] = "keep-alive";

        // Bounded channel bridges bus publish calls to response writer
        var channel = Channel.CreateBounded<(string type, string data)>(
            new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });

        using var sub = eventBus.Subscribe(id, (type, data) =>
        {
            channel.Writer.TryWrite((type, data));
            return Task.CompletedTask;
        });

        // Connected signal
        await WriteSseAsync("heartbeat", "connected", ct);
        await Response.Body.FlushAsync(ct);

        using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(20));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                while (channel.Reader.TryRead(out var ev))
                    await WriteSseAsync(ev.type, ev.data, ct);

                await Response.Body.FlushAsync(ct);

                var nextTick = heartbeat.WaitForNextTickAsync(ct).AsTask();
                var hasData  = channel.Reader.WaitToReadAsync(ct).AsTask();
                var done     = await Task.WhenAny(nextTick, hasData);

                if (done == nextTick && !ct.IsCancellationRequested)
                {
                    await WriteSseAsync("heartbeat", "ping", ct);
                    await Response.Body.FlushAsync(ct);
                }
            }
        }
        catch (OperationCanceledException) { /* client disconnected — normal exit */ }
    }

    private async Task WriteSseAsync(string eventType, string data, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes($"event: {eventType}\ndata: {data}\n\n");
        await Response.Body.WriteAsync(bytes, ct);
    }

    // ── Simulation endpoints ───────────────────────────────────────────────────

    [HttpPost("{id:guid}/simulate/start")]
    public async Task<ActionResult<SimulationStatusDto>> SimulationStart(
        Guid id,
        [FromBody] StartSimulationRequest req,
        [FromServices] SimulationEngine sim)
    {
        var p = await FindOwned(id);
        if (p is null) return NotFound();
        if (string.IsNullOrWhiteSpace(p.ProjectPath)) return UnprocessableEntity("Producto sin projectPath.");

        var validScenarios = new[] { "hora_pico", "cocina_congestionada", "bajo_inventario", "operacion_normal" };
        var scenario = req.Scenario?.Trim().ToLowerInvariant() ?? "operacion_normal";
        if (!validScenarios.Contains(scenario))
            return UnprocessableEntity($"Escenario inválido: {scenario}");

        if (sim.IsRunning(id)) return UnprocessableEntity("Ya hay una simulación activa para este producto.");

        // Persist SimulationRun
        var run = new SimulationRun { ProductId = id, Scenario = scenario };
        db.SimulationRuns.Add(run);

        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId = id,
            EventType = ActivityType.SimulationStarted,
            Title     = $"Simulación iniciada — escenario: {scenario}",
            Details   = $"Escenario: {scenario}",
        });

        await db.SaveChangesAsync();

        sim.Start(id, run.Id, p.ProjectPath, scenario);
        await eventBus.PingAsync(id);

        return Ok(new SimulationStatusDto(true, scenario, 0, run.Id, run.StartedAt));
    }

    [HttpPost("{id:guid}/simulate/stop")]
    public async Task<ActionResult<SimulationStatusDto>> SimulationStop(
        Guid id,
        [FromServices] SimulationEngine sim)
    {
        if (!await OwnsProduct(id)) return NotFound();

        if (!sim.IsRunning(id)) return UnprocessableEntity("No hay simulación activa.");

        var opsGenerated = sim.Stop(id);

        // Update the latest running SimulationRun
        var run = await db.SimulationRuns
            .Where(r => r.ProductId == id && r.Status == "running")
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        if (run is not null)
        {
            run.Status       = "stopped";
            run.OpsGenerated = opsGenerated;
            run.StoppedAt    = DateTime.UtcNow;
        }

        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId = id,
            EventType = ActivityType.SimulationStopped,
            Title     = $"Simulación detenida — {opsGenerated} operaciones generadas",
            Details   = $"Ops: {opsGenerated}",
        });

        await db.SaveChangesAsync();
        await eventBus.PingAsync(id);

        return Ok(new SimulationStatusDto(false, run?.Scenario, opsGenerated, run?.Id, run?.StartedAt));
    }

    [HttpGet("{id:guid}/simulate/status")]
    public async Task<ActionResult<SimulationStatusDto>> SimulationStatus(
        Guid id,
        [FromServices] SimulationEngine sim)
    {
        if (!await OwnsProduct(id)) return NotFound();

        var status = sim.GetStatus(id);
        if (status is null)
        {
            var lastRun = await db.SimulationRuns
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.StartedAt)
                .FirstOrDefaultAsync();
            return Ok(new SimulationStatusDto(false, lastRun?.Scenario, lastRun?.OpsGenerated ?? 0, lastRun?.Id, lastRun?.StartedAt));
        }

        return Ok(new SimulationStatusDto(true, status.Value.scenario, status.Value.opsGenerated, null, null));
    }

    // POST /api/products/{id}/demo/reset
    [HttpPost("{id}/demo/reset")]
    public async Task<ActionResult<DemoResetDto>> DemoReset(
        Guid id,
        [FromServices] SimulationEngine sim,
        [FromServices] DemoResetEngine demoReset,
        CancellationToken ct)
    {
        var product = await db.Products.FindAsync([id], ct);
        if (product is null) return NotFound();
        if (string.IsNullOrEmpty(product.ProjectPath)) return BadRequest("Product has no project path.");

        // Stop active simulation so reset data isn't immediately overwritten
        if (sim.GetStatus(id) is not null)
            sim.Stop(id);

        var apiBase = $"{Request.Scheme}://{Request.Host}";
        await demoReset.ResetAsync(product.ProjectPath, id, apiBase, ct);

        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId = id,
            EventType = ActivityType.DemoReset,
            Title     = "Demo reset — datos restaurados al estado inicial",
        });
        await db.SaveChangesAsync(ct);

        return Ok(new DemoResetDto(true, "Demo data reset to canonical lunch-service state."));
    }

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private static ProductSummaryDto ToSummaryDto(Product p) =>
        new(p.Id, p.Name, p.Status.ToString(), p.PreviewUrl, p.PreviewStatus, p.PreviewPort,
            p.IsProcessing, p.RuntimePhase, p.ProjectPath, p.ScaffoldStatus, p.RuntimeHealth,
            p.CreatedAt, p.UpdatedAt, p.DeployStatus, p.DeployUrl, p.DeployedAt, null);

    private static ProductDetailDto ToDetailDto(Product p) =>
        new(
            p.Id, p.Name, p.Prompt, p.Status.ToString(), p.PreviewUrl,
            p.PreviewStatus, p.PreviewPort, p.PreviewLastStartedAt, p.PreviewError,
            p.IsProcessing, p.RuntimePhase, p.ProjectPath, p.ScaffoldStatus, p.RuntimeHealth,
            p.CreatedAt, p.UpdatedAt,
            p.Messages.Select(m => new MessageDto(m.Id, m.Role.ToString(), m.Content, m.DetectedIntent, m.Confidence, m.CreatedAt)),
            p.ActivityEvents.Select(a => new ActivityDto(a.Id, a.EventType.ToString(), a.Title, a.Details, a.ArtifactId, a.CreatedAt)),
            p.Approvals.Select(a => new ApprovalDto(a.Id, a.Title, a.Description, a.Status.ToString(), a.ResolutionNote, a.ArtifactId, a.CreatedAt, a.ResolvedAt)),
            p.Memory
                .GroupBy(m => m.Key)
                .Select(g => g.OrderByDescending(m => m.CreatedAt).First())
                .Select(m => new MemoryDto(m.Key, m.Value, m.CreatedAt)),
            p.Artifacts
                .Where(a => a.Status != ArtifactStatus.Superseded)
                .Select(a => new ArtifactSummaryDto(a.Id, a.Type, a.Title, a.Version, a.Status.ToString(), a.GeneratedAt)),
            p.ScaffoldEntries
                .Select(s => new ScaffoldEntryDto(s.Id, s.RelativePath, s.EntryType, s.Language, s.SortOrder)),
            p.ScaffoldChanges
                .Select(s => new ScaffoldChangeDto(s.Id, s.ChangeType, s.TargetPath, s.ModuleLabel, s.Layer, s.CreatedAt)),
            p.Modules
                .Where(m => m.IsActive)
                .Select(m => new ProductModuleDto(m.Id, m.ModuleName, m.EntityName, m.RoutePath, m.ControllerName, m.Layer, m.Source, m.IsActive, m.DetectedAt)),
            p.FileRevisions
                .Select(r => new FileRevisionDto(r.Id, r.RelativePath, r.PatchType, r.Reason, r.BeforeContent != null, r.CreatedAt)),
            p.ValidationRuns
                .OrderByDescending(r => r.StartedAt).Take(5)
                .Select(r => new ValidationRunSummaryDto(r.Id, r.Status, r.StartedAt, r.FinishedAt, r.AutofixAttempts, r.GatesPassed, r.GatesFailed)),
            p.DeployStatus, p.DeployUrl, p.DeployedAt, p.DeployCommitHash, p.DeployBranch,
            p.DeployRuns
                .OrderByDescending(r => r.StartedAt).Take(5)
                .Select(r => new DeployRunSummaryDto(r.Id, r.Status, r.StartedAt, r.FinishedAt, r.DeployUrl, r.CommitHash, r.Branch)),
            p.RefactorRecommendations
                .Select(r => new RefactorRecommendationDto(r.Id, r.Type, r.Title, r.Severity, r.Reason, r.Impact, r.Risk, r.Status, r.Note, r.ArtifactId, r.CreatedAt, r.ResolvedAt, r.ExecutedAt, r.ExecutionError))
        );
}
