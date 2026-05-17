using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BuilderPlatform.Infrastructure.Services;

public class AutofixEngine(
    ILogger<AutofixEngine> logger,
    ProjectAwarenessEngine awarenessEngine,
    PreviewRunner previewRunner)
{
    // ── Public API ─────────────────────────────────────────────────────────────

    public async Task<(bool Fixed, string Action)> TryFixGateAsync(
        GateResult gate, Product product, AppDbContext db, CancellationToken ct)
    {
        return gate.Gate switch
        {
            "registry_nav"       => await FixRegistryAsync("frontend/registry/nav-items.json", gate.Gate, product, db, ct),
            "registry_dashboard" => await FixRegistryAsync("frontend/registry/dashboard.json",  gate.Gate, product, db, ct),
            "registry_modules"   => await FixRegistryAsync("frontend/registry/modules.json",    gate.Gate, product, db, ct),
            "preview_running"    => await FixPreviewAsync(product, ct),
            "dashboard_route"    => await FixPreviewAsync(product, ct),
            _                    => (false, $"Sin autofix para {gate.Gate}"),
        };
    }

    // ── Registry fix ───────────────────────────────────────────────────────────

    private async Task<(bool, string)> FixRegistryAsync(
        string relPath, string gate, Product product, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(product.ProjectPath))
            return (false, "Sin projectPath");

        // Strategy 1 — restore BeforeContent from the latest FileRevision
        var relPathLower = relPath.ToLowerInvariant();
        var lastRev = await db.FileRevisions
            .Where(r => r.ProductId == product.Id &&
                        r.RelativePath.ToLower() == relPathLower)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (lastRev?.BeforeContent != null)
        {
            try
            {
                JsonDocument.Parse(lastRev.BeforeContent); // validate before writing
                var fullPath = Path.Combine(product.ProjectPath, relPath.Replace('/', Path.DirectorySeparatorChar));
                await File.WriteAllTextAsync(fullPath, lastRev.BeforeContent,
                    new System.Text.UTF8Encoding(false), ct);
                logger.LogInformation("Autofix: restored {Gate} from revision {RevId}", gate, lastRev.Id);
                return (true, $"Registry restaurado desde revisión anterior ({lastRev.CreatedAt:HH:mm:ss})");
            }
            catch (JsonException)
            {
                logger.LogWarning("Autofix: BeforeContent for {Gate} is also invalid JSON — trying regeneration", gate);
            }
        }

        // Strategy 2 — regenerate from project scan (nav + modules only)
        if (gate is "registry_nav" or "registry_modules")
        {
            try
            {
                await awarenessEngine.ScanAndRegisterAsync(product, db, ct);
                logger.LogInformation("Autofix: regenerated {Gate} via project scan", gate);
                return (true, "Registry regenerado desde escaneo del proyecto");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Autofix: scan-based regeneration failed for {Gate}", gate);
                return (false, $"Error en regeneración: {ex.Message[..Math.Min(ex.Message.Length, 80)]}");
            }
        }

        return (false, "Sin revisión anterior disponible para restaurar");
    }

    // ── Preview fix ────────────────────────────────────────────────────────────

    private async Task<(bool, string)> FixPreviewAsync(Product product, CancellationToken ct)
    {
        // Stop if currently running (stale or crashed process)
        var (currentStatus, _, _) = previewRunner.GetLiveStatus(product.Id);
        if (currentStatus == "running")
        {
            await previewRunner.StopAsync(product.Id);
            await Task.Delay(2000, ct);
        }

        previewRunner.StartAsync(product.Id);

        // Poll for up to 65s (Next.js cold start can take ~50s)
        var deadline = DateTime.UtcNow.AddSeconds(65);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(3000, ct);
            var (status, _, port) = previewRunner.GetLiveStatus(product.Id);
            if (status == "running")
            {
                logger.LogInformation("Autofix: preview restarted on port {Port} for product {Id}", port, product.Id);
                return (true, $"Preview reiniciado en puerto {port}");
            }
        }

        return (false, "Preview no se levantó en 65s — posible error de build o puerto");
    }
}
