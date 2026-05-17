using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BuilderPlatform.Infrastructure.Services;

public enum PatchOperation
{
    NavPushDashboardFirst,
    DashboardPremiumUpgrade,
    DashboardAddQuickStats,
    Unknown,
}

public record PatchResult(bool Success, string Message, string? FilePath = null);

public record ManagedFileInfo(
    string   RelativePath,
    string   DisplayName,
    string   FileType,
    bool     Exists,
    string?  LastModified,
    bool     IsEditable,
    int      RevisionCount);

public class RuntimePatchEngine(ILogger<RuntimePatchEngine> logger)
{
    // ── Managed zones ──────────────────────────────────────────────────────────

    private static readonly ManagedFileInfo[] ManagedFileDefinitions =
    [
        new("frontend/registry/nav-items.json",  "Navegación (nav-items)",     "json", false, null, true,  0),
        new("frontend/registry/dashboard.json",   "Dashboard Registry",         "json", false, null, true,  0),
        new("frontend/registry/modules.json",     "Módulos Registry",           "json", false, null, false, 0),
        new("frontend/app/globals.css",           "Theme / CSS Variables",      "css",  false, null, true,  0),
        new("frontend/app/dashboard/page.tsx",    "Dashboard Page",             "tsx",  false, null, true,  0),
        new("frontend/app/layout.tsx",            "Root Layout",                "tsx",  false, null, false, 0),
        new("frontend/next.config.ts",            "Next.js Config",             "tsx",  false, null, false, 0),
    ];

    public static bool IsManagedPath(string relativePath) =>
        ManagedFileDefinitions.Any(f => f.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)) ||
        (relativePath.StartsWith("frontend/components/widgets/", StringComparison.OrdinalIgnoreCase) &&
         relativePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase));

    public static bool IsEditablePath(string relativePath) =>
        ManagedFileDefinitions.FirstOrDefault(f =>
            f.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase))?.IsEditable == true;

    public List<ManagedFileInfo> GetManagedFiles(string projectPath, IReadOnlyList<FileRevision> revisions)
    {
        var result = new List<ManagedFileInfo>();

        foreach (var def in ManagedFileDefinitions)
        {
            var fullPath = Path.Combine(projectPath, def.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var exists   = File.Exists(fullPath);
            var lastMod  = exists ? File.GetLastWriteTimeUtc(fullPath).ToString("yyyy-MM-ddTHH:mm:ssZ") : null;
            var revCount = revisions.Count(r => r.RelativePath.Equals(def.RelativePath, StringComparison.OrdinalIgnoreCase));

            result.Add(def with { Exists = exists, LastModified = lastMod, RevisionCount = revCount });
        }

        // Add widget files
        var widgetsDir = Path.Combine(projectPath, "frontend", "components", "widgets");
        if (Directory.Exists(widgetsDir))
        {
            foreach (var widgetFile in Directory.GetFiles(widgetsDir, "*.tsx"))
            {
                var rel      = "frontend/components/widgets/" + Path.GetFileName(widgetFile);
                var lastMod  = File.GetLastWriteTimeUtc(widgetFile).ToString("yyyy-MM-ddTHH:mm:ssZ");
                var revCount = revisions.Count(r => r.RelativePath.Equals(rel, StringComparison.OrdinalIgnoreCase));
                result.Add(new ManagedFileInfo(rel, Path.GetFileNameWithoutExtension(widgetFile), "tsx", true, lastMod, false, revCount));
            }
        }

        return result;
    }

    // ── Patch classification ───────────────────────────────────────────────────

    public static PatchOperation ClassifyPatch(string content)
    {
        var lower = content.ToLowerInvariant();

        if (lower.Contains("dashboard primero") || lower.Contains("primero en el sidebar") ||
            lower.Contains("primero en el nav") || lower.Contains("reordena el sidebar") ||
            lower.Contains("reordena la navegación") || lower.Contains("reordena el nav"))
            return PatchOperation.NavPushDashboardFirst;

        if (lower.Contains("quick stats") || lower.Contains("estadísticas rápidas") ||
            lower.Contains("stats arriba") || lower.Contains("métricas rápidas"))
            return PatchOperation.DashboardAddQuickStats;

        if (lower.Contains("más premium") || lower.Contains("mas premium") ||
            lower.Contains("haz premium") || lower.Contains("aspecto premium") ||
            lower.Contains("mejora visual") || lower.Contains("look premium"))
            return PatchOperation.DashboardPremiumUpgrade;

        return PatchOperation.Unknown;
    }

    // ── Patch execution ────────────────────────────────────────────────────────

    public async Task<PatchResult> ApplyPatchAsync(
        Product product, PatchOperation op, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(product.ProjectPath))
            return new PatchResult(false, "El producto no tiene un proyecto generado.");

        return op switch
        {
            PatchOperation.NavPushDashboardFirst   => await PatchNavReorderAsync(product, db, ct),
            PatchOperation.DashboardPremiumUpgrade => await PatchDashboardPremiumAsync(product, db, ct),
            PatchOperation.DashboardAddQuickStats  => await PatchDashboardQuickStatsAsync(product, db, ct),
            _                                      => new PatchResult(false, "Operación de patch no reconocida."),
        };
    }

    // ── Nav reorder ────────────────────────────────────────────────────────────

    private async Task<PatchResult> PatchNavReorderAsync(Product product, AppDbContext db, CancellationToken ct)
    {
        const string relPath = "frontend/registry/nav-items.json";
        var fullPath = Path.Combine(product.ProjectPath!, "frontend", "registry", "nav-items.json");

        if (!File.Exists(fullPath))
            return new PatchResult(false, "nav-items.json no encontrado.");

        try
        {
            var before  = await File.ReadAllTextAsync(fullPath, ct);
            var items   = JsonSerializer.Deserialize<List<JsonElement>>(before) ?? [];
            var opts    = new JsonSerializerOptions { WriteIndented = true };

            // Find dashboard item and move to front (or add it if missing)
            var dashIdx = items.FindIndex(i =>
                i.TryGetProperty("href", out var h) &&
                h.GetString()?.Equals("/dashboard", StringComparison.OrdinalIgnoreCase) == true);

            if (dashIdx == 0)
                return new PatchResult(false, "Dashboard ya es el primero en el sidebar.", relPath);

            if (dashIdx > 0)
            {
                var dashItem = items[dashIdx];
                items.RemoveAt(dashIdx);
                items.Insert(0, dashItem);
            }
            else
            {
                // Not in nav at all — add it
                var dashEntry = JsonSerializer.SerializeToElement(new { label = "Dashboard", href = "/dashboard", icon = "Home" });
                items.Insert(0, dashEntry);
            }

            var after = JsonSerializer.Serialize(items, opts);
            await File.WriteAllTextAsync(fullPath, after, new System.Text.UTF8Encoding(false), ct);

            SaveRevision(db, product.Id, relPath, "nav_reorder",
                "Dashboard movido a primera posición en el sidebar", before, after);

            logger.LogInformation("NavReorder applied for product {Id}", product.Id);
            return new PatchResult(true, "Dashboard movido a primera posición en el sidebar.", relPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NavReorder failed for product {Id}", product.Id);
            return new PatchResult(false, $"Error al reordenar nav: {ex.Message}");
        }
    }

    // ── Dashboard premium upgrade ──────────────────────────────────────────────

    private async Task<PatchResult> PatchDashboardPremiumAsync(Product product, AppDbContext db, CancellationToken ct)
    {
        const string relPath = "frontend/app/dashboard/page.tsx";
        var fullPath = Path.Combine(product.ProjectPath!, "frontend", "app", "dashboard", "page.tsx");

        if (!File.Exists(fullPath))
            return new PatchResult(false, "Dashboard page.tsx no encontrado.");

        try
        {
            var before = await File.ReadAllTextAsync(fullPath, ct);
            var after  = PremiumDashboardPage(product.Name);

            await File.WriteAllTextAsync(fullPath, after, new System.Text.UTF8Encoding(false), ct);

            SaveRevision(db, product.Id, relPath, "dashboard_premium",
                "Dashboard actualizado a layout premium", before, after);

            logger.LogInformation("DashboardPremium applied for product {Id}", product.Id);
            return new PatchResult(true, "Dashboard actualizado a layout premium con cards de colores y sección de estado.", relPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DashboardPremium failed for product {Id}", product.Id);
            return new PatchResult(false, $"Error al actualizar dashboard: {ex.Message}");
        }
    }

    // ── Dashboard quick stats ──────────────────────────────────────────────────

    private async Task<PatchResult> PatchDashboardQuickStatsAsync(Product product, AppDbContext db, CancellationToken ct)
    {
        const string relPath = "frontend/app/dashboard/page.tsx";
        var fullPath = Path.Combine(product.ProjectPath!, "frontend", "app", "dashboard", "page.tsx");

        if (!File.Exists(fullPath))
            return new PatchResult(false, "Dashboard page.tsx no encontrado.");

        try
        {
            var before = await File.ReadAllTextAsync(fullPath, ct);

            // Idempotency check — skip if already has quick stats
            if (before.Contains("quickStats") || before.Contains("quick-stats"))
                return new PatchResult(false, "Dashboard ya tiene quick stats.", relPath);

            var after = QuickStatsDashboardPage(product.Name);

            await File.WriteAllTextAsync(fullPath, after, new System.Text.UTF8Encoding(false), ct);

            SaveRevision(db, product.Id, relPath, "dashboard_quick_stats",
                "Quick stats bar agregada al dashboard", before, after);

            logger.LogInformation("DashboardQuickStats applied for product {Id}", product.Id);
            return new PatchResult(true, "Quick stats bar agregada al tope del dashboard.", relPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DashboardQuickStats failed for product {Id}", product.Id);
            return new PatchResult(false, $"Error al agregar quick stats: {ex.Message}");
        }
    }

    // ── DB helper ─────────────────────────────────────────────────────────────

    private static void SaveRevision(AppDbContext db, Guid productId, string relPath, string patchType,
        string reason, string before, string after)
        => db.FileRevisions.Add(new FileRevision
        {
            ProductId     = productId,
            RelativePath  = relPath,
            PatchType     = patchType,
            Reason        = reason,
            BeforeContent = before.Length > 8000 ? before[..8000] + "\n…[truncated]" : before,
            AfterContent  = after.Length  > 8000 ? after[..8000]  + "\n…[truncated]" : after,
        });

    // ── Premium dashboard template ─────────────────────────────────────────────

    private static string PremiumDashboardPage(string productName) =>
        """
        export default function DashboardPage() {
          const stats = [
            { label: "Registros activos",     value: "—", color: "var(--status-active-text)", bg: "var(--status-active-bg)"  },
            { label: "Pendientes de revisión", value: "—", color: "var(--status-warn-text)",   bg: "var(--status-warn-bg)"    },
            { label: "Métricas del mes",       value: "—", color: "var(--status-info-text)",   bg: "var(--status-info-bg)"    },
            { label: "Alertas críticas",       value: "—", color: "var(--status-danger-text)", bg: "var(--status-danger-bg)"  },
          ];
          return (
            <div style={{ padding: "28px" }}>
              <div style={{ marginBottom: "28px" }}>
                <h1 style={{ fontSize: "24px", fontWeight: "800", color: "var(--foreground)", marginBottom: "4px" }}>
                  __DISPLAY_NAME__ — Dashboard
                </h1>
                <p style={{ fontSize: "13px", color: "var(--muted)" }}>Resumen operativo del sistema</p>
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))", gap: "16px", marginBottom: "32px" }}>
                {stats.map((s) => (
                  <div key={s.label} style={{ padding: "22px", background: s.bg, borderRadius: "14px", border: "1px solid var(--border)" }}>
                    <p style={{ fontSize: "11px", color: s.color, marginBottom: "10px", textTransform: "uppercase", letterSpacing: "0.5px", fontWeight: "600" }}>{s.label}</p>
                    <p style={{ fontSize: "32px", fontWeight: "800", color: s.color }}>{s.value}</p>
                  </div>
                ))}
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "16px" }}>
                <div style={{ padding: "24px", background: "var(--surface)", borderRadius: "14px", border: "1px solid var(--border)" }}>
                  <p style={{ fontSize: "11px", color: "var(--muted)", marginBottom: "14px", textTransform: "uppercase", letterSpacing: "0.4px" }}>Estado del Sistema</p>
                  <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                    <div style={{ width: "8px", height: "8px", borderRadius: "50%", background: "var(--status-active-text)", flexShrink: 0 }} />
                    <span style={{ fontSize: "13px", color: "var(--foreground)" }}>Sistema operativo</span>
                  </div>
                </div>
                <div style={{ padding: "24px", background: "var(--surface)", borderRadius: "14px", border: "1px solid var(--border)" }}>
                  <p style={{ fontSize: "11px", color: "var(--muted)", marginBottom: "14px", textTransform: "uppercase", letterSpacing: "0.4px" }}>Actividad Reciente</p>
                  <p style={{ fontSize: "13px", color: "var(--muted)" }}>Sin actividad reciente</p>
                </div>
              </div>
            </div>
          );
        }
        """.Replace("__DISPLAY_NAME__", productName.Replace("\"", "&quot;"));

    // ── Quick stats dashboard template ─────────────────────────────────────────

    private static string QuickStatsDashboardPage(string productName) =>
        """
        export default function DashboardPage() {
          const quickStats = [
            { label: "Hoy",    value: "—" },
            { label: "Semana", value: "—" },
            { label: "Mes",    value: "—" },
            { label: "Total",  value: "—" },
          ];
          const stats = [
            { label: "Total Registros", value: "—" },
            { label: "Activos",         value: "—" },
            { label: "Pendientes",      value: "—" },
            { label: "Completados",     value: "—" },
          ];
          return (
            <div style={{ padding: "28px" }}>
              <h1 style={{ fontSize: "22px", fontWeight: "700", color: "var(--foreground)", marginBottom: "20px" }}>
                __DISPLAY_NAME__ — Dashboard
              </h1>
              <div style={{ display: "flex", marginBottom: "24px", background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)", overflow: "hidden" }}>
                {quickStats.map((q, i) => (
                  <div key={q.label} style={{ flex: 1, textAlign: "center", padding: "16px 12px", borderRight: i < quickStats.length - 1 ? "1px solid var(--border)" : "none" }}>
                    <p style={{ fontSize: "10px", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.4px", marginBottom: "6px" }}>{q.label}</p>
                    <p style={{ fontSize: "22px", fontWeight: "700", color: "var(--foreground)" }}>{q.value}</p>
                  </div>
                ))}
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))", gap: "16px" }}>
                {stats.map((s) => (
                  <div key={s.label} style={{ padding: "20px", background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)" }}>
                    <p style={{ fontSize: "11px", color: "var(--muted)", marginBottom: "8px", textTransform: "uppercase" }}>{s.label}</p>
                    <p style={{ fontSize: "28px", fontWeight: "700", color: "var(--status-info-text)" }}>{s.value}</p>
                  </div>
                ))}
              </div>
            </div>
          );
        }
        """.Replace("__DISPLAY_NAME__", productName.Replace("\"", "&quot;"));
}
