using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuilderPlatform.Infrastructure.Services;

// ── Evolution data model (stored as JSON in ProductMemory key "evolution_memory") ──

public record EvolutionModule(string Name, string Route, string Layer, DateTime AddedAt);

public record EvolutionRelation(
    string From, string To, string RelationType, string Reason, DateTime DetectedAt);

public record EvolutionDecision(string Summary, DateTime MadeAt);

public record EvolutionContext(
    List<EvolutionModule>   Modules,
    List<EvolutionRelation> Relations,
    List<EvolutionDecision> Decisions,
    List<string>            FeatureHistory)
{
    public static EvolutionContext Empty() => new([], [], [], []);
}

// ─────────────────────────────────────────────────────────────────────────────

public class ProductEvolutionService
{
    private const string MemoryKey = "evolution_memory";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented          = false,
    };

    // Domain relationship rules — pure keyword matching, zero ML/embeddings
    // (NewFeatureKeywords, ExistingModuleKeywords, relationType, reason)
    private static readonly (string[] NewKw, string[] ExistingKw, string Type, string Reason)[] Rules =
    [
        (["compra", "orden de compra", "ordenar compra", "purchase", "procurement"],
         ["proveedor", "inventario", "stock", "bodega"],
         "integra_con",
         "Las órdenes de compra referencian proveedores e impactan el inventario"),

        (["factura", "facturacion", "cobro", "billing", "cuenta por cobrar"],
         ["pedido", "cliente", "orden", "venta", "comanda"],
         "consolida",
         "La facturación consolida pedidos y datos del cliente"),

        (["inventario", "stock", "almacen", "bodega", "existencia", "bodega"],
         ["proveedor", "compra", "producto", "menu", "menú"],
         "abastecido_por",
         "El inventario se alimenta de compras a proveedores y se consume en el menú"),

        (["pedido", "orden", "comanda", "venta", "ticket"],
         ["mesa", "cliente", "menu", "menú", "inventario", "cocina"],
         "referencia",
         "Los pedidos conectan mesas, menú e inventario en el flujo operacional"),

        (["reserva", "reservacion", "booking", "cita", "appointment"],
         ["mesa", "cliente", "empleado", "sala", "recurso"],
         "asigna",
         "Las reservaciones asignan recursos y requieren datos del cliente"),

        (["empleado", "staff", "personal", "trabajador", "operador", "usuario"],
         ["turno", "planilla", "horario", "rol", "sucursal", "area"],
         "gestiona",
         "Los empleados tienen turnos, horarios y rol en planilla"),

        (["cliente", "customer", "paciente", "estudiante"],
         ["pedido", "factura", "reserva", "cita", "servicio", "historial"],
         "genera",
         "Los clientes generan pedidos, facturas y reservaciones"),

        (["menu", "menú", "carta", "catalogo", "producto"],
         ["inventario", "categoria", "precio", "ingrediente"],
         "consume",
         "El menú consume inventario y se organiza por categorías"),

        (["pago", "cobro", "transaccion", "transferencia", "cargo"],
         ["cliente", "pedido", "factura", "cuenta"],
         "liquida",
         "Los pagos liquidan pedidos y facturas de clientes"),

        (["reporte", "report", "analytics", "estadistica", "metrica", "kpi"],
         ["*todos*"],
         "consolida",
         "Los reportes consolidan datos de todos los módulos del producto"),

        (["notificacion", "alerta", "aviso", "email", "mensaje"],
         ["pedido", "inventario", "cliente", "empleado", "reserva"],
         "monitorea",
         "Las notificaciones monitorizan eventos de otros módulos"),

        (["proveedor", "supplier", "abastecedor", "distribuidor"],
         ["inventario", "compra", "producto", "bodega"],
         "abastece",
         "Los proveedores abastecen el inventario a través de órdenes de compra"),

        (["turno", "horario", "schedule", "jornada"],
         ["empleado", "area", "sucursal", "puesto"],
         "asigna",
         "Los turnos asignan empleados a áreas y jornadas específicas"),

        (["cocina", "kitchen", "produccion", "despacho"],
         ["pedido", "menu", "menú", "inventario", "orden"],
         "procesa",
         "La cocina procesa pedidos y consume ítems del menú e inventario"),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<EvolutionContext> GetEvolutionContextAsync(Guid productId, AppDbContext db)
    {
        var json = await db.ProductMemories
            .Where(m => m.ProductId == productId && m.Key == MemoryKey)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.Value)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(json)) return EvolutionContext.Empty();
        try { return JsonSerializer.Deserialize<EvolutionContext>(json, JsonOpts) ?? EvolutionContext.Empty(); }
        catch   { return EvolutionContext.Empty(); }
    }

    /// <summary>Detect cross-module relations for a new feature against the existing context.</summary>
    public List<EvolutionRelation> DetectRelations(string newFeatureName, EvolutionContext ctx)
    {
        if (ctx.Modules.Count == 0) return [];

        var newNorm  = Norm(newFeatureName);
        var relations = new List<EvolutionRelation>();
        var addedTo   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Rule-based detection
        foreach (var (newKw, existingKw, rType, reason) in Rules)
        {
            if (!newKw.Any(kw => newNorm.Contains(Norm(kw)))) continue;

            foreach (var mod in ctx.Modules)
            {
                if (addedTo.Contains(mod.Name)) continue;
                var modNorm = Norm(mod.Name);

                var matched = existingKw.Contains("*todos*")
                    || existingKw.Any(kw => modNorm.Contains(Norm(kw)));

                if (!matched) continue;

                relations.Add(new EvolutionRelation(
                    From: newFeatureName, To: mod.Name,
                    RelationType: rType, Reason: reason,
                    DetectedAt: DateTime.UtcNow));
                addedTo.Add(mod.Name);
            }
        }

        // Fallback: shared meaningful domain words (≥4 chars)
        string[] stopwords = ["para", "con", "del", "los", "las", "sus", "que", "por",
                               "una", "cion", "ment", "tion", "ando", "endo", "ista"];
        var newWords = newNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4 && !stopwords.Contains(w))
            .ToHashSet();

        if (newWords.Count > 0)
        {
            foreach (var mod in ctx.Modules)
            {
                if (addedTo.Contains(mod.Name)) continue;
                var modWords = Norm(mod.Name)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 4 && !stopwords.Contains(w));

                if (!newWords.Overlaps(modWords)) continue;

                relations.Add(new EvolutionRelation(
                    From: newFeatureName, To: mod.Name,
                    RelationType: "comparte_concepto",
                    Reason: $"{newFeatureName} y {mod.Name} comparten conceptos del mismo dominio",
                    DetectedAt: DateTime.UtcNow));
                addedTo.Add(mod.Name);
            }
        }

        return relations;
    }

    /// <summary>
    /// Record a newly generated feature in the evolution memory.
    /// Pass existingCtx from a prior GetEvolutionContextAsync call to avoid a second DB read.
    /// Does NOT call SaveChangesAsync — the caller's transaction handles that.
    /// </summary>
    public void RecordFeature(Guid productId, string featureName, string route,
        List<EvolutionRelation> relations, EvolutionContext existingCtx, AppDbContext db)
    {
        var ctx = existingCtx;

        // Add module if not already present
        if (!ctx.Modules.Any(m => string.Equals(m.Name, featureName, StringComparison.OrdinalIgnoreCase)))
            ctx.Modules.Add(new EvolutionModule(featureName, $"/{route}", "full-stack", DateTime.UtcNow));

        // Add new relations (deduplicated)
        foreach (var rel in relations)
        {
            var exists = ctx.Relations.Any(r =>
                string.Equals(r.From, rel.From, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.To,   rel.To,   StringComparison.OrdinalIgnoreCase));
            if (!exists) ctx.Relations.Add(rel);
        }

        // History
        if (!ctx.FeatureHistory.Any(h => string.Equals(h, featureName, StringComparison.OrdinalIgnoreCase)))
            ctx.FeatureHistory.Add(featureName);

        // Architectural decision log
        var decision = relations.Count > 0
            ? $"Feature {featureName} generada y conectada evolutivamente con: {string.Join(", ", relations.Select(r => r.To))}"
            : $"Feature {featureName} generada como módulo independiente";
        ctx.Decisions.Add(new EvolutionDecision(decision, DateTime.UtcNow));

        PersistContext(productId, ctx, db);
    }

    /// <summary>
    /// Initialize evolution memory after scaffold completes.
    /// Does NOT call SaveChangesAsync.
    /// </summary>
    public void RecordScaffold(Guid productId, string industry, List<ProductModule> modules, AppDbContext db)
    {
        var ctx = EvolutionContext.Empty();

        foreach (var mod in modules)
        {
            ctx.Modules.Add(new EvolutionModule(
                mod.ModuleName,
                mod.RoutePath ?? $"/{Norm(mod.ModuleName).Replace(" ", "-")}",
                mod.Layer ?? "full-stack",
                DateTime.UtcNow));
        }

        ctx.FeatureHistory.AddRange(modules.Select(m => m.ModuleName));
        ctx.Decisions.Add(new EvolutionDecision(
            $"Scaffold inicial: {modules.Count} módulo(s) generados con Clean Architecture (.NET 9 + Next.js 15) · industria: {industry}",
            DateTime.UtcNow));

        PersistContext(productId, ctx, db);
    }

    public string BuildContextSummary(EvolutionContext ctx)
    {
        if (ctx.Modules.Count == 0) return "Sin módulos registrados todavía.";
        var names = string.Join(", ", ctx.Modules.Select(m => m.Name));
        return $"Módulos actuales: {names}. Relaciones: {ctx.Relations.Count}. Historial: {ctx.FeatureHistory.Count} feature(s).";
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    public void PersistEvolutionContext(Guid productId, EvolutionContext ctx, AppDbContext db)
        => PersistContext(productId, ctx, db);

    private static void PersistContext(Guid productId, EvolutionContext ctx, AppDbContext db)
    {
        var json = JsonSerializer.Serialize(ctx, JsonOpts);
        db.ProductMemories.Add(new ProductMemory
        {
            ProductId = productId,
            Key       = MemoryKey,
            Value     = json,
        });
    }

    private static string Norm(string s) => s
        .ToLowerInvariant()
        .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
        .Replace("ñ", "n").Replace("ü", "u");
}
