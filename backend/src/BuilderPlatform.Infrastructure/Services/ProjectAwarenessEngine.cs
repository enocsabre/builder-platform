using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BuilderPlatform.Infrastructure.Services;

public class ProjectAwarenessEngine(ILogger<ProjectAwarenessEngine> logger)
{
    // ── Public API ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProductModule>> ScanAndRegisterAsync(
        Product product, AppDbContext db, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(product.ProjectPath) || !Directory.Exists(product.ProjectPath))
            return [];

        var safeName = ToSafeName(product.Name);
        var modules  = ScanProjectModules(product, safeName);

        // Replace previous scaffold scan results
        var existing = await db.ProductModules
            .Where(m => m.ProductId == product.Id && m.Source == "scaffold")
            .ToListAsync(ct);
        db.ProductModules.RemoveRange(existing);
        foreach (var m in modules) db.ProductModules.Add(m);

        // Persist registry JSON files
        await WriteRegistryFilesAsync(product.ProjectPath, modules, ct);

        logger.LogInformation("ProjectAwareness: scanned {Count} modules for product {Id}", modules.Count, product.Id);
        return modules;
    }

    public async Task RegisterDeltaModuleAsync(
        Product product, string featureName, AppDbContext db,
        IReadOnlyList<ScaffoldChange> changes, CancellationToken ct = default)
    {
        var pascal = ToPascalCase(featureName);

        var existing = await db.ProductModules
            .FirstOrDefaultAsync(m => m.ProductId == product.Id && m.ModuleName == pascal, ct);
        if (existing is not null) return;

        var route  = ToDeltaRoute(featureName);
        var module = new ProductModule
        {
            ProductId      = product.Id,
            ModuleName     = pascal,
            EntityName     = pascal,
            RoutePath      = $"/{route}",
            ControllerName = $"{pascal}Controller",
            Layer          = "full-stack",
            Source         = "delta",
        };
        db.ProductModules.Add(module);

        if (!string.IsNullOrWhiteSpace(product.ProjectPath) && Directory.Exists(product.ProjectPath))
            await AppendModuleToRegistryAsync(product.ProjectPath, module, ct);
    }

    public async Task<bool> ModuleExistsAsync(Guid productId, string featureName, AppDbContext db, CancellationToken ct = default)
    {
        var pascal = ToPascalCase(featureName);
        return await db.ProductModules
            .AnyAsync(m => m.ProductId == productId && m.ModuleName == pascal, ct);
    }

    public async Task<(bool hasDashboard, bool widgetExists)> CheckDashboardAsync(
        string projectPath, string widgetName, CancellationToken ct = default)
    {
        var dashPath = Path.Combine(projectPath, "frontend", "registry", "dashboard.json");
        if (!File.Exists(dashPath)) return (false, false);

        try
        {
            var json = await File.ReadAllTextAsync(dashPath, ct);
            var doc  = JsonSerializer.Deserialize<JsonElement>(json);

            if (!doc.TryGetProperty("hasMainDashboard", out var hasProp) || !hasProp.GetBoolean())
                return (false, false);

            var normalizedName = ToPascalCase(NormalizeForPath(widgetName));
            if (doc.TryGetProperty("widgets", out var widgets))
            {
                foreach (var w in widgets.EnumerateArray())
                {
                    if (w.TryGetProperty("name", out var n) &&
                        n.GetString()?.Equals(normalizedName, StringComparison.OrdinalIgnoreCase) == true)
                        return (true, true);
                }
            }
            return (true, false);
        }
        catch { return (false, false); }
    }

    public async Task<string> AddWidgetToRegistryAsync(
        string projectPath, string widgetDisplayName, string componentPath, CancellationToken ct = default)
    {
        var dashPath = Path.Combine(projectPath, "frontend", "registry", "dashboard.json");
        if (!File.Exists(dashPath)) return componentPath;

        try
        {
            var json = await File.ReadAllTextAsync(dashPath, ct);
            var doc  = JsonSerializer.Deserialize<JsonElement>(json);
            var opts = new JsonSerializerOptions { WriteIndented = true };

            var widgets = new List<object>();
            if (doc.TryGetProperty("widgets", out var existing))
                foreach (var w in existing.EnumerateArray())
                    widgets.Add(w);

            widgets.Add(new
            {
                id            = Guid.NewGuid().ToString(),
                name          = ToPascalCase(NormalizeForPath(widgetDisplayName)),
                displayName   = widgetDisplayName,
                componentPath = componentPath,
                type          = "metrics",
                addedAt       = DateTime.UtcNow.ToString("O"),
            });

            var updated = new
            {
                hasMainDashboard = doc.GetProperty("hasMainDashboard").GetBoolean(),
                widgets          = widgets,
            };
            await File.WriteAllTextAsync(dashPath, JsonSerializer.Serialize(updated, opts), new System.Text.UTF8Encoding(false), ct);
        }
        catch { /* Best-effort */ }

        return componentPath;
    }

    public async Task<bool> CheckAndUpdateNavAsync(
        string projectPath, string routePath, string label, CancellationToken ct = default)
    {
        var navPath = Path.Combine(projectPath, "frontend", "registry", "nav-items.json");
        if (!File.Exists(navPath)) return false;

        try
        {
            var json = await File.ReadAllTextAsync(navPath, ct);
            var doc  = JsonSerializer.Deserialize<JsonElement>(json);
            var opts = new JsonSerializerOptions { WriteIndented = true };

            var items = new List<object>();
            foreach (var item in doc.EnumerateArray())
            {
                items.Add(item);
                if (item.TryGetProperty("href", out var h) &&
                    h.GetString()?.Equals(routePath, StringComparison.OrdinalIgnoreCase) == true)
                    return false; // Already exists — skip
            }

            items.Add(new { label, href = routePath, icon = "Grid" });
            await File.WriteAllTextAsync(navPath, JsonSerializer.Serialize(items, opts), new System.Text.UTF8Encoding(false), ct);
            return true; // Added
        }
        catch { return false; }
    }

    // ── Scanning ───────────────────────────────────────────────────────────────

    private static List<ProductModule> ScanProjectModules(Product product, string safeName)
    {
        var modules = new List<ProductModule>();
        var root    = product.ProjectPath!;

        var entitiesDir    = Path.Combine(root, "backend", "src", $"{safeName}.Domain", "Entities");
        var controllersDir = Path.Combine(root, "backend", "src", $"{safeName}.API",    "Controllers");
        var frontendAppDir = Path.Combine(root, "frontend", "app");

        var entityNames    = ScanEntityNames(entitiesDir);
        var controllerSet  = ScanControllerSet(controllersDir);
        var frontendRoutes = ScanFrontendRoutes(frontendAppDir);

        foreach (var entity in entityNames)
        {
            var controller = ResolveController(entity, controllerSet);
            var route      = ResolveRoute(entity, frontendRoutes);

            modules.Add(new ProductModule
            {
                ProductId      = product.Id,
                ModuleName     = entity,
                EntityName     = entity,
                RoutePath      = route,
                ControllerName = controller,
                Layer          = "full-stack",
                Source         = "scaffold",
            });
        }

        return modules;
    }

    private static IReadOnlyList<string> ScanEntityNames(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null && !n.EndsWith("Status") && !n.EndsWith("Enum") && !n.EndsWith("Type"))
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList();
    }

    private static HashSet<string> ScanControllerSet(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*Controller.cs")
            .Select(f => Path.GetFileNameWithoutExtension(f) ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ScanFrontendRoutes(string appDir)
    {
        if (!Directory.Exists(appDir)) return [];
        return Directory.GetFiles(appDir, "page.tsx", SearchOption.AllDirectories)
            .Select(f => Path.GetDirectoryName(f))
            .Where(d => d is not null)
            .Select(d => "/" + Path.GetRelativePath(appDir, d!).Replace("\\", "/"))
            .Where(r => r != "/" && !r.Contains("[") && !r.Contains("("))
            .OrderBy(r => r)
            .ToList();
    }

    private static string ResolveController(string entity, HashSet<string> controllerSet)
    {
        if (controllerSet.Contains($"{entity}sController"))  return $"{entity}sController";
        if (controllerSet.Contains($"{entity}Controller"))   return $"{entity}Controller";
        var suffix = entity.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? "" : "s";
        return $"{entity}{suffix}Controller";
    }

    private static string ResolveRoute(string entity, IReadOnlyList<string> routes)
    {
        var lower = entity.ToLowerInvariant();
        var match = routes.FirstOrDefault(r =>
            r.Equals("/" + lower + "s", StringComparison.OrdinalIgnoreCase) ||
            r.Equals("/" + lower,      StringComparison.OrdinalIgnoreCase));
        var fallback = lower.EndsWith("s") ? $"/{lower}" : $"/{lower}s";
        return match ?? fallback;
    }

    // ── Registry files ─────────────────────────────────────────────────────────

    private static async Task WriteRegistryFilesAsync(
        string projectPath, IReadOnlyList<ProductModule> modules, CancellationToken ct)
    {
        var registryDir = Path.Combine(projectPath, "frontend", "registry");
        Directory.CreateDirectory(registryDir);

        var opts = new JsonSerializerOptions { WriteIndented = true };

        // Load human-readable entity labels if written by ScaffoldEngine
        var entityLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var labelsPath   = Path.Combine(registryDir, "entity-labels.json");
        if (File.Exists(labelsPath))
        {
            try
            {
                var raw = await File.ReadAllTextAsync(labelsPath, ct);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
                if (loaded is not null)
                    foreach (var kv in loaded) entityLabels[kv.Key] = kv.Value;
            }
            catch { /* ignore malformed labels — fall back to entity name */ }
        }

        var modulesPayload = modules.Select(m => new
        {
            name       = m.ModuleName,
            entity     = m.EntityName,
            route      = m.RoutePath,
            controller = m.ControllerName,
            layer      = m.Layer,
            source     = m.Source,
        });
        await File.WriteAllTextAsync(
            Path.Combine(registryDir, "modules.json"),
            JsonSerializer.Serialize(modulesPayload, opts), new System.Text.UTF8Encoding(false), ct);

        // nav-items — use actual scanned route directories, not entity-name fallbacks
        var feAppDir    = Path.Combine(projectPath, "frontend", "app");
        var reservedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/dashboard", "/login" };
        var actualRoutes = ScanFrontendRoutes(feAppDir)
            .Where(r => !reservedSet.Contains(r))
            .ToList();

        var navItems = actualRoutes.Select(r =>
        {
            var match = modules.FirstOrDefault(m =>
                string.Equals(m.RoutePath, r, StringComparison.OrdinalIgnoreCase));
            var label = match is not null &&
                        entityLabels.TryGetValue(match.EntityName ?? match.ModuleName, out var hl)
                            ? hl
                            : PrettifyRoute(r);
            return new { label, href = r, icon = "Grid" };
        });
        await File.WriteAllTextAsync(
            Path.Combine(registryDir, "nav-items.json"),
            JsonSerializer.Serialize(navItems, opts), new System.Text.UTF8Encoding(false), ct);

        var hasDashboard = Directory.Exists(Path.Combine(projectPath, "frontend", "app", "dashboard"));
        var dashboard = new
        {
            hasMainDashboard = hasDashboard,
            widgets          = modules.Select(m => new { module = m.ModuleName, type = "summary" }),
        };
        await File.WriteAllTextAsync(
            Path.Combine(registryDir, "dashboard.json"),
            JsonSerializer.Serialize(dashboard, opts), new System.Text.UTF8Encoding(false), ct);
    }

    private static async Task AppendModuleToRegistryAsync(
        string projectPath, ProductModule module, CancellationToken ct)
    {
        var modulesPath = Path.Combine(projectPath, "frontend", "registry", "modules.json");
        if (!File.Exists(modulesPath)) return;

        try
        {
            var json    = await File.ReadAllTextAsync(modulesPath, ct);
            var list    = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];
            var newItem = JsonSerializer.SerializeToElement(new
            {
                name       = module.ModuleName,
                entity     = module.EntityName,
                route      = module.RoutePath,
                controller = module.ControllerName,
                layer      = module.Layer,
                source     = module.Source,
            });
            list.Add(newItem);
            await File.WriteAllTextAsync(modulesPath,
                JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false), ct);
        }
        catch { /* Registry append is best-effort */ }
    }

    // ── Static helpers (mirror ScaffoldEngine naming logic) ───────────────────

    private static string PrettifyRoute(string route) =>
        string.Join(" ", route.TrimStart('/').Split('-')
            .Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : ""));

    internal static string NormalizeForPath(string s) =>
        s.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
         .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U")
         .Replace("ñ", "n").Replace("Ñ", "N").Replace("ü", "u").Replace("Ü", "U");

    private static string ToSafeName(string name) =>
        string.Concat(
            name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));

    private static string ToPascalCase(string s) =>
        string.Concat(
            s.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
             .Where(w => w.Length > 0)
             .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));

    private static string ToDeltaRoute(string featureName)
    {
        var lower = featureName.ToLowerInvariant()
            .Replace("gestión de ", "").Replace("gestión ", "")
            .Replace("módulo de ", "").Replace("módulo ", "");
        var first = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(w => w.Length >= 3) ?? "module";
        return Regex.Replace(first, @"[^a-z0-9-]", "");
    }
}
