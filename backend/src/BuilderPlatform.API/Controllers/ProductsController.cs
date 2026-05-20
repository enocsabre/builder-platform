using BuilderPlatform.API.DTOs;
using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using BuilderPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Threading.Channels;

namespace BuilderPlatform.API.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(AppDbContext db, RuntimeOrchestrator orchestrator, PreviewRunner previewRunner, RuntimeEventBus eventBus) : ControllerBase
{
    // GET /api/products
    [HttpGet]
    public async Task<IEnumerable<ProductSummaryDto>> GetAll()
    {
        return await db.Products
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new ProductSummaryDto(
                p.Id, p.Name, p.Status.ToString(), p.PreviewUrl, p.PreviewStatus, p.PreviewPort,
                p.IsProcessing, p.RuntimePhase, p.ProjectPath, p.ScaffoldStatus, p.RuntimeHealth,
                p.CreatedAt, p.UpdatedAt,
                p.DeployStatus, p.DeployUrl, p.DeployedAt))
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
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);

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
            product.DeployStatus, product.DeployUrl, product.DeployedAt);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, dto);
    }

    // PATCH /api/products/{id}/status
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ProductSummaryDto>> UpdateStatus(Guid id, UpdateProductStatusRequest req)
    {
        var product = await db.Products.FindAsync(id);
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
        var product = await db.Products.FindAsync(id);
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
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/products/{id}/messages
    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(Guid id, SendMessageRequest req)
    {
        var product = await db.Products.FindAsync(id);
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
        var product = await db.Products.FindAsync(id);
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
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();

        await previewRunner.StopAsync(id);
        return Ok(new { message = "Preview detenido" });
    }

    // POST /api/products/{id}/open-vscode
    [HttpPost("{id:guid}/open-vscode")]
    public async Task<IActionResult> OpenInVSCode(Guid id, [FromServices] IConfiguration config)
    {
        var product = await db.Products.FindAsync(id);
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
            .FirstOrDefaultAsync(p => p.Id == id);

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
        var product = await db.Products.FindAsync(id);
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
        var product = await db.Products.FindAsync(id);
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
        var run = await db.DeployRuns
            .FirstOrDefaultAsync(r => r.Id == runId && r.ProductId == id);

        if (run is null) return NotFound();

        var gates = System.Text.Json.JsonSerializer.Deserialize<List<GateResultDto>>(run.GateResults ?? "[]") ?? [];

        return Ok(new DeployRunDetailDto(
            run.Id, run.Status, run.StartedAt, run.FinishedAt,
            run.Logs, run.Errors, run.DeployUrl, run.CommitHash, run.Branch, gates));
    }

    // GET /api/products/{id}/events  — SSE stream for real-time updates
    [HttpGet("{id:guid}/events")]
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

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private static ProductSummaryDto ToSummaryDto(Product p) =>
        new(p.Id, p.Name, p.Status.ToString(), p.PreviewUrl, p.PreviewStatus, p.PreviewPort,
            p.IsProcessing, p.RuntimePhase, p.ProjectPath, p.ScaffoldStatus, p.RuntimeHealth,
            p.CreatedAt, p.UpdatedAt, p.DeployStatus, p.DeployUrl, p.DeployedAt);

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
                .Select(r => new DeployRunSummaryDto(r.Id, r.Status, r.StartedAt, r.FinishedAt, r.DeployUrl, r.CommitHash, r.Branch))
        );
}
