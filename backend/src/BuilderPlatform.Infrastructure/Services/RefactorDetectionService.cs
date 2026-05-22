using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuilderPlatform.Infrastructure.Services;

// ── Refactor detection rules ──────────────────────────────────────────────────
//
// All detection is pure keyword/structural logic — no ML, no embeddings.
// Rules are additive and non-destructive: we only detect and recommend.
// The caller decides whether to persist or act on the results.
//
// Severity scale:
//   high   — clear architectural problem, likely to cause confusion or bugs
//   medium — architectural smell, worth addressing in next sprint
//   low    — cosmetic/naming issue, low urgency

public class RefactorDetectionService
{
    private static readonly string[] Stopwords =
        ["para", "con", "del", "los", "las", "sus", "que", "por",
         "una", "cion", "ment", "tion", "ando", "endo", "ista",
         "the", "and", "for", "from", "with", "that", "this"];

    private static readonly string[] AdminPrefixes =
        ["gestion de ", "gestion-de-", "gestión de ", "gestión-de-",
         "manejo de ", "manejo-de-",
         "administracion de ", "administracion-de-",
         "administración de ", "administración-de-",
         "modulo de ", "módulo de ", "modulo-de-", "módulo-de-",
         "sistema de ", "sistema-de-"];

    // Known pairs that should be connected but often aren't
    private static readonly (string[] A, string[] B, string Reason)[] MissingConnectionRules =
    [
        (["proveedor", "proveedores", "supplier"],
         ["inventario", "stock", "almacen", "bodega"],
         "Los proveedores abastecen el inventario — conectarlos permite trazabilidad de compras"),

        (["factura", "facturacion", "billing"],
         ["pedido", "orden", "venta"],
         "La facturación depende de pedidos — la relación debe ser explícita en el modelo"),

        (["empleado", "personal", "staff"],
         ["turno", "horario", "jornada"],
         "Los empleados tienen turnos — modelar esta relación mejora la integridad del sistema"),

        (["cliente", "customer"],
         ["pedido", "orden", "reserva"],
         "Los clientes generan pedidos y reservas — la relación debe estar explícita"),

        (["cocina", "kitchen"],
         ["pedido", "orden", "comanda"],
         "La cocina procesa pedidos — la relación es fundamental en el flujo operacional"),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Detect architectural issues in the evolution context.
    /// Persists NEW recommendations to the change tracker.
    /// Does NOT call SaveChangesAsync — caller handles that.
    /// Returns all new recommendations detected this run.
    /// </summary>
    public async Task<List<RefactorRecommendation>> DetectAndPersistAsync(
        Guid productId, EvolutionContext ctx, AppDbContext db)
    {
        if (ctx.Modules.Count < 2) return [];

        // Load pending titles to deduplicate
        var existingTitles = await db.RefactorRecommendations
            .Where(r => r.ProductId == productId && r.Status == "pending")
            .Select(r => r.Title)
            .ToListAsync();

        var detected = Detect(ctx);
        var added    = new List<RefactorRecommendation>();

        foreach (var rec in detected)
        {
            if (existingTitles.Contains(rec.Title, StringComparer.OrdinalIgnoreCase))
                continue;
            rec.ProductId = productId;
            db.RefactorRecommendations.Add(rec);
            added.Add(rec);
        }

        return added;
    }

    // ── Detection rules ───────────────────────────────────────────────────────

    private List<RefactorRecommendation> Detect(EvolutionContext ctx)
    {
        var recs = new List<RefactorRecommendation>();

        var modules = ctx.Modules;

        // Rule 1: Duplicate / similar module names
        for (var i = 0; i < modules.Count; i++)
        {
            for (var j = i + 1; j < modules.Count; j++)
            {
                var a = modules[i];
                var b = modules[j];

                if (!AreSimilar(a.Name, b.Name)) continue;

                recs.Add(new RefactorRecommendation
                {
                    Type     = "duplicate_module",
                    Title    = $"Consolidar '{a.Name}' y '{b.Name}'",
                    Severity = "high",
                    Reason   = $"Los módulos '{a.Name}' y '{b.Name}' tienen nombres muy similares y pueden estar modelando el mismo concepto del dominio.",
                    Impact   = "Consolidar ambos módulos reduce duplicidad, simplifica el modelo de datos y mejora la coherencia de la API.",
                    Risk     = "Bajo si se hace en sprint dedicado: refactor de rutas + merge de entidades. Sin cambios destructivos si solo se renombra primero.",
                });
            }
        }

        // Rule 2: Redundant admin-prefix names
        foreach (var mod in modules)
        {
            var normName = Norm(mod.Name);
            var matched  = AdminPrefixes.FirstOrDefault(p => normName.StartsWith(Norm(p)));
            if (matched is null) continue;

            var suggested = TitleCase(normName[Norm(matched).Length..].Trim());
            if (suggested.Length < 3) continue;

            recs.Add(new RefactorRecommendation
            {
                Type     = "redundant_name",
                Title    = $"Renombrar '{mod.Name}' a '{suggested}'",
                Severity = "medium",
                Reason   = $"El prefijo '{matched.Trim()}' es redundante — todos los módulos ya son de gestión por naturaleza.",
                Impact   = "Nombre más limpio en sidebar, rutas más cortas, mejor legibilidad del código generado.",
                Risk     = "Bajo: solo renombrar el módulo + actualizar nav-items.json y registry.",
            });
        }

        // Rule 3: Ugly / long routes
        foreach (var mod in modules)
        {
            var route = mod.Route.TrimStart('/');

            var isLong      = route.Length > 22;
            var hasAdminSeg = AdminPrefixes.Any(p => route.Contains(Norm(p).Replace(" ", "-").TrimEnd('-')));

            if (!isLong && !hasAdminSeg) continue;

            recs.Add(new RefactorRecommendation
            {
                Type     = "ugly_route",
                Title    = $"Simplificar ruta '/{route}'",
                Severity = "low",
                Reason   = $"La ruta '/{route}' es demasiado larga o contiene prefijos innecesarios que afectan la URL y el sidebar.",
                Impact   = "URLs más limpias, mejor experiencia en sidebar, rutas más fáciles de recordar.",
                Risk     = "Bajo: solo actualizar nav-items.json. Sin cambios en controllers ni entidades.",
            });
        }

        // Rule 4: Contradictory bidirectional relations
        for (var i = 0; i < ctx.Relations.Count; i++)
        {
            for (var j = i + 1; j < ctx.Relations.Count; j++)
            {
                var a = ctx.Relations[i];
                var b = ctx.Relations[j];

                // A→B with typeX and B→A with typeY (different types = potential contradiction)
                var isBidirectional =
                    (string.Equals(a.From, b.To, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(a.To,   b.From, StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(a.RelationType, b.RelationType, StringComparison.OrdinalIgnoreCase));

                if (!isBidirectional) continue;

                recs.Add(new RefactorRecommendation
                {
                    Type     = "contradictory_relation",
                    Title    = $"Revisar relación bidireccional '{a.From}' ↔ '{a.To}'",
                    Severity = "high",
                    Reason   = $"'{a.From}' → '{a.To}' ({a.RelationType}) y '{b.From}' → '{b.To}' ({b.RelationType}) se contradicen. Relaciones bidireccionales de tipos distintos suelen indicar ambigüedad en el dominio.",
                    Impact   = "Clarificar la dirección dominante de la relación simplifica la lógica de negocio y el modelo de datos.",
                    Risk     = "Medio: requiere revisar la semántica del dominio antes de decidir cuál dirección preservar.",
                });
            }
        }

        // Rule 5: Missing obvious connections
        foreach (var (aKws, bKws, reason) in MissingConnectionRules)
        {
            var modA = modules.FirstOrDefault(m => aKws.Any(kw => Norm(m.Name).Contains(kw)));
            var modB = modules.FirstOrDefault(m => bKws.Any(kw => Norm(m.Name).Contains(kw)));

            if (modA is null || modB is null) continue;

            // Skip if already related
            var alreadyConnected = ctx.Relations.Any(r =>
                (string.Equals(r.From, modA.Name, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(r.To,   modB.Name, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(r.From, modB.Name, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(r.To,   modA.Name, StringComparison.OrdinalIgnoreCase)));

            if (alreadyConnected) continue;

            recs.Add(new RefactorRecommendation
            {
                Type     = "missing_connection",
                Title    = $"Conectar '{modA.Name}' con '{modB.Name}'",
                Severity = "medium",
                Reason   = reason,
                Impact   = "La relación explícita mejora la coherencia del modelo, permite trazar flujos de negocio y facilita la generación de reportes cruzados.",
                Risk     = "Bajo: solo registrar la relación en evolution memory. Sin cambios de código destructivos.",
            });
        }

        // Rule 6: Feature history divergence (module in history but missing from modules)
        var moduleNames = modules.Select(m => Norm(m.Name)).ToHashSet();
        foreach (var feat in ctx.FeatureHistory)
        {
            if (moduleNames.Contains(Norm(feat))) continue;

            recs.Add(new RefactorRecommendation
            {
                Type     = "orphaned_history",
                Title    = $"Reconciliar '{feat}' — en historial pero sin módulo activo",
                Severity = "low",
                Reason   = $"'{feat}' aparece en el historial de features pero no tiene un módulo registrado activo. Puede indicar que el módulo fue renombrado o eliminado sin actualizar el registry.",
                Impact   = "El historial y el registry deben estar sincronizados para que la evolution memory sea confiable.",
                Risk     = "Bajo: solo actualizar el registry o agregar el módulo faltante.",
            });
        }

        return recs;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool AreSimilar(string a, string b)
    {
        var na = Norm(a);
        var nb = Norm(b);

        // One contains the other (e.g. "Proveedores" vs "Gestion de Proveedores")
        if (na.Contains(nb) || nb.Contains(na)) return true;

        // Shared meaningful words
        var wa = MeaningfulWords(na);
        var wb = MeaningfulWords(nb);

        if (wa.Count == 0 || wb.Count == 0) return false;

        var shared = wa.Intersect(wb).Count();
        var smaller = Math.Min(wa.Count, wb.Count);

        // ≥50% of the smaller set's words are shared
        return shared > 0 && (double)shared / smaller >= 0.5;
    }

    private static HashSet<string> MeaningfulWords(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4 && !Stopwords.Contains(w))
            .ToHashSet();

    private static string Norm(string s) => s
        .ToLowerInvariant()
        .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
        .Replace("ñ", "n").Replace("ü", "u");

    private static string TitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s[1..];
    }
}
