using BuilderPlatform.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BuilderPlatform.Infrastructure.Services;

public class RefactorExecutionService(ILogger<RefactorExecutionService> logger)
{
    // ── Public types ──────────────────────────────────────────────────────────

    public record FileBackup(string RelPath, string FullPath, string Before, string After);
    public record ExecutionResult(bool Success, string? Error, List<FileBackup> Backups);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>True when this refactor type can be safely auto-applied to runtime-managed files.</summary>
    public bool CanExecuteSafely(string type) => type is
        "redundant_name" or "duplicate_module" or "orphaned_history" or
        "missing_connection" or "contradictory_relation";

    /// <summary>
    /// Apply safe file-level changes for the given recommendation.
    /// Files are written to disk immediately — the caller must validate + rollback if needed.
    /// Does NOT touch AppDbContext. Returns backups for rollback.
    /// </summary>
    public async Task<ExecutionResult> ExecuteFileChangesAsync(string projectPath, RefactorRecommendation rec)
    {
        var backups = new List<FileBackup>();

        try
        {
            switch (rec.Type)
            {
                case "redundant_name":
                {
                    var m = Regex.Match(rec.Title, @"Renombrar '(.+?)' a '(.+?)'");
                    if (!m.Success)
                        return Fail("No se pudo parsear el título de la recomendación para ejecución.");
                    var (from, to) = (m.Groups[1].Value, m.Groups[2].Value);

                    await TryRenameNavLabel(projectPath, from, to, backups);
                    await TryRenameEntityLabel(projectPath, from, to, backups);
                    break;
                }
                case "duplicate_module":
                {
                    var m = Regex.Match(rec.Title, @"Consolidar '(.+?)' y '(.+?)'");
                    if (!m.Success)
                        return Fail("No se pudo parsear el título de la recomendación para ejecución.");
                    var (redundant, clean) = (m.Groups[1].Value, m.Groups[2].Value);

                    // Mark the redundant nav item as "(fusionar)" — don't delete, just label it
                    await TryRenameNavLabel(projectPath, redundant, $"{clean} (fusionar)", backups);
                    await TryRenameEntityLabel(projectPath, redundant, clean, backups);
                    break;
                }
                // orphaned_history, missing_connection, contradictory_relation:
                // only evolution memory — no file changes needed here
            }

            return new ExecutionResult(true, null, backups);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RefactorExecution file-changes failed for rec {RecId}", rec.Id);
            await RollbackAsync(backups);
            return Fail(ex.Message);
        }
    }

    /// <summary>Restore files to their pre-execution content. Best-effort.</summary>
    public async Task RollbackAsync(List<FileBackup> backups)
    {
        foreach (var b in backups)
        {
            try { await File.WriteAllTextAsync(b.FullPath, b.Before, Utf8NoBom); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Rollback failed for {Path}", b.FullPath);
            }
        }
    }

    /// <summary>Validate registry JSON files after execution. Fast check — JSON parse only.</summary>
    public async Task<bool> ValidateRegistriesAsync(string projectPath)
    {
        var toCheck = new[] { "frontend/registry/nav-items.json", "frontend/registry/modules.json" };
        foreach (var rel in toCheck)
        {
            var full = Path.Combine(projectPath, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) continue;
            try
            {
                var content = await File.ReadAllTextAsync(full);
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
            }
            catch { return false; }
        }
        return true;
    }

    // ── Private file transformers ─────────────────────────────────────────────

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private static readonly JsonSerializerOptions JsonWriteOpts = new() { WriteIndented = true };

    private static async Task TryRenameNavLabel(
        string projectPath, string from, string to, List<FileBackup> backups)
    {
        const string rel = "frontend/registry/nav-items.json";
        var full = Path.Combine(projectPath, rel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full)) return;

        var before = await File.ReadAllTextAsync(full, Encoding.UTF8);
        var arr    = JsonNode.Parse(before)!.AsArray();

        var changed = false;
        foreach (var item in arr)
        {
            if (item?["label"]?.GetValue<string>() is { } label &&
                string.Equals(label, from, StringComparison.OrdinalIgnoreCase))
            {
                item["label"] = JsonValue.Create(to);
                changed = true;
            }
        }
        if (!changed) return;

        var after = arr.ToJsonString(JsonWriteOpts);
        await File.WriteAllTextAsync(full, after, Utf8NoBom);
        backups.Add(new FileBackup(rel, full, before, after));
    }

    private static async Task TryRenameEntityLabel(
        string projectPath, string from, string to, List<FileBackup> backups)
    {
        const string rel = "frontend/registry/entity-labels.json";
        var full = Path.Combine(projectPath, rel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full)) return;

        var before = await File.ReadAllTextAsync(full, Encoding.UTF8);
        var obj    = JsonNode.Parse(before)!.AsObject();

        // Update entries where VALUE matches `from` (key = internal entity name, value = display label)
        var keysToUpdate = obj
            .Where(kv => string.Equals(kv.Value?.GetValue<string>(), from, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();
        if (keysToUpdate.Count == 0) return;

        foreach (var key in keysToUpdate)
            obj[key] = JsonValue.Create(to);

        var after = obj.ToJsonString(JsonWriteOpts);
        await File.WriteAllTextAsync(full, after, Utf8NoBom);
        backups.Add(new FileBackup(rel, full, before, after));
    }

    private static ExecutionResult Fail(string error) => new(false, error, []);
}
