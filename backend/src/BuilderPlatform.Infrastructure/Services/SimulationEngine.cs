using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace BuilderPlatform.Infrastructure.Services;

/// <summary>
/// Generates realistic restaurant operations into the product's .data/ directory.
/// File names and column keys match the generated scaffold's page expectations.
/// </summary>
public class SimulationEngine(ILogger<SimulationEngine> logger, RuntimeEventBus bus)
{
    private sealed class SimState
    {
        public string  Scenario        { get; init; } = "operacion_normal";
        public string  ProjectPath     { get; init; } = string.Empty;
        public Guid    ProductId       { get; init; }
        public Guid    RunId           { get; init; }
        public int     OpsGenerated;
        public CancellationTokenSource Cts { get; } = new();
    }

    private readonly ConcurrentDictionary<Guid, SimState> _running = new();

    private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = true };
    private static readonly System.Text.Encoding  Utf8NoBom = new System.Text.UTF8Encoding(false);

    private static readonly Dictionary<string, int> TickMs = new()
    {
        ["hora_pico"]             = 2500,
        ["cocina_congestionada"]  = 3000,
        ["bajo_inventario"]       = 3500,
        ["operacion_normal"]      = 4000,
    };

    // ── Public API ────────────────────────────────────────────────────────────

    public bool IsRunning(Guid productId) => _running.ContainsKey(productId);

    public (string scenario, int opsGenerated)? GetStatus(Guid productId)
    {
        if (_running.TryGetValue(productId, out var s))
            return (s.Scenario, s.OpsGenerated);
        return null;
    }

    public bool Start(Guid productId, Guid runId, string projectPath, string scenario)
    {
        if (_running.ContainsKey(productId)) return false;

        var state = new SimState
        {
            Scenario    = scenario,
            ProjectPath = projectPath,
            ProductId   = productId,
            RunId       = runId,
        };

        if (!_running.TryAdd(productId, state)) return false;

        _ = Task.Run(() => RunLoopAsync(state));
        return true;
    }

    public int Stop(Guid productId)
    {
        if (!_running.TryRemove(productId, out var state)) return 0;
        state.Cts.Cancel();
        return state.OpsGenerated;
    }

    // ── Background loop ───────────────────────────────────────────────────────

    private async Task RunLoopAsync(SimState state)
    {
        var tick = TickMs.GetValueOrDefault(state.Scenario, 4000);
        try
        {
            while (!state.Cts.Token.IsCancellationRequested)
            {
                await Task.Delay(tick, state.Cts.Token);
                if (state.Cts.Token.IsCancellationRequested) break;

                await GenerateOperationAsync(state);
                Interlocked.Increment(ref state.OpsGenerated);
                await bus.PingAsync(state.ProductId);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SimulationEngine loop error for product {Id}", state.ProductId);
        }
    }

    private async Task GenerateOperationAsync(SimState state)
    {
        var rnd = Random.Shared;
        var dataDir = Path.Combine(state.ProjectPath, "frontend", ".data");
        Directory.CreateDirectory(dataDir);

        switch (state.Scenario)
        {
            case "hora_pico":
                await AppendPedidoAsync(dataDir, rnd, highVolume: true);
                if (rnd.NextDouble() < 0.6) await UpdateMesaAsync(dataDir, rnd);
                if (rnd.NextDouble() < 0.4) await AppendKitchenDisplayAsync(dataDir, rnd, busy: true);
                if (rnd.NextDouble() < 0.15) await UpdateInventoryAsync(dataDir, rnd);
                await AppendActivityAsync(dataDir, rnd, state.Scenario);
                break;

            case "cocina_congestionada":
                await AppendKitchenDisplayAsync(dataDir, rnd, busy: true);
                if (rnd.NextDouble() < 0.3) await AppendPedidoAsync(dataDir, rnd, highVolume: false);
                if (rnd.NextDouble() < 0.5) await UpdateMesaAsync(dataDir, rnd);
                await AppendActivityAsync(dataDir, rnd, state.Scenario);
                break;

            case "bajo_inventario":
                await UpdateInventoryAsync(dataDir, rnd);
                if (rnd.NextDouble() < 0.4) await AppendPedidoAsync(dataDir, rnd, highVolume: false);
                await AppendActivityAsync(dataDir, rnd, state.Scenario);
                break;

            default: // operacion_normal
                var roll = rnd.NextDouble();
                if (roll < 0.40)      await AppendPedidoAsync(dataDir, rnd, highVolume: false);
                else if (roll < 0.65) await UpdateMesaAsync(dataDir, rnd);
                else if (roll < 0.85) await AppendKitchenDisplayAsync(dataDir, rnd, busy: false);
                else                  await UpdateInventoryAsync(dataDir, rnd);
                await AppendActivityAsync(dataDir, rnd, state.Scenario);
                break;
        }
    }

    // ── File-level operations ─────────────────────────────────────────────────
    // All field names match the generated scaffold's page column definitions.

    private static readonly string[] PedidoStatuses = ["Pendiente", "Preparando", "En camino", "Entregado"];
    private static readonly string[] MenuItems = [
        "Casado con pollo", "Arroz con leche", "Sopa negra", "Gallo pinto",
        "Chifrijo", "Patacones", "Ensalada de palmito", "Limonada natural",
        "Agua mineral", "Café chorreado",
    ];
    private static readonly string[] Servers = ["Ana G.", "Luis R.", "María C.", "Carlos A.", "Sofía M."];

    private static string PedidoSc(string estado) => estado switch
    {
        "Preparando" => "warn",
        "En camino"  => "info",
        "Entregado"  => "active",
        "Cerrado"    => "active",
        _            => "info",   // Pendiente
    };

    // pedidos-comandas.json — columns: Orden, Mesa, Ítems, Total, Hora, Estado
    private static async Task AppendPedidoAsync(string dataDir, Random rnd, bool highVolume)
    {
        const string file = "pedidos-comandas.json";
        var arr  = await ReadArrayAsync(Path.Combine(dataDir, file));

        // Advance existing simulated pedidos through workflow
        foreach (var item in arr.ToList())
        {
            if (item?["_simulated"]?.GetValue<bool>() != true) continue;
            var st  = item["Estado"]?.GetValue<string>() ?? "";
            var idx = Array.IndexOf(PedidoStatuses, st);
            if (idx >= 0 && idx < PedidoStatuses.Length - 1)
            {
                var next = PedidoStatuses[idx + 1];
                item["Estado"] = JsonValue.Create(next);
                item["_sc"]    = JsonValue.Create(PedidoSc(next));
                item["_updatedAt"] = JsonValue.Create(DateTime.UtcNow.ToString("o"));
            }
        }

        int newOrders = highVolume ? rnd.Next(1, 4) : 1;
        for (int i = 0; i < newOrders; i++)
        {
            var orderNum = $"#{rnd.Next(1200, 9999)}";
            var mesa     = $"Mesa {rnd.Next(1, 13)}";
            var items    = MenuItems[rnd.Next(MenuItems.Length)];
            if (rnd.NextDouble() < 0.3) items += $", {MenuItems[rnd.Next(MenuItems.Length)]}";
            var total = rnd.Next(3500, 18000);
            var hora  = DateTime.Now.ToString("HH:mm");

            arr.Add(new JsonObject
            {
                ["Orden"]       = orderNum,
                ["Mesa"]        = mesa,
                ["Ítems"]       = items,
                ["Total"]       = total.ToString(),
                ["Hora"]        = hora,
                ["Estado"]      = "Pendiente",
                ["_sc"]         = "info",
                ["_simulated"]  = true,
                ["_createdAt"]  = DateTime.UtcNow.ToString("o"),
                ["_updatedAt"]  = DateTime.UtcNow.ToString("o"),
            });
        }

        // Keep max 100 records — trim oldest Entregado first to preserve revenue history
        TrimSimulated(arr, 100);
        await WriteArrayAsync(Path.Combine(dataDir, file), arr);
    }

    // mesas.json — columns: Mesa, Capacidad, Estado, Ocupada desde, Mesero
    private static async Task UpdateMesaAsync(string dataDir, Random rnd)
    {
        const string file = "mesas.json";
        var arr = await ReadArrayAsync(Path.Combine(dataDir, file));

        // Ensure 12 mesas exist
        var existing = arr.Select(t => t?["Mesa"]?.GetValue<string>() ?? "").ToHashSet();
        for (int n = 1; n <= 12; n++)
        {
            var name = $"Mesa {n}";
            if (existing.Contains(name)) continue;
            arr.Add(new JsonObject
            {
                ["Mesa"]         = name,
                ["Capacidad"]    = (n % 3 == 0) ? "6 pers." : (n % 2 == 0) ? "4 pers." : "2 pers.",
                ["Estado"]       = "Disponible",
                ["Ocupada desde"] = "—",
                ["Mesero"]       = "—",
                ["_sc"]          = "active",
                ["_simulated"]   = true,
            });
        }

        // Flip a random simulated mesa
        var targets = arr.Where(t => t?["_simulated"]?.GetValue<bool>() == true).ToList();
        if (targets.Count > 0)
        {
            var pick = targets[rnd.Next(targets.Count)];
            if (pick != null)
            {
                var current = pick["Estado"]?.GetValue<string>() ?? "Disponible";
                var (newEstado, newSc, desde, mesero) = current switch
                {
                    "Disponible"   => rnd.NextDouble() < 0.75
                                       ? ("Ocupada",   "warn", $"hace {rnd.Next(1,45)} min", Servers[rnd.Next(Servers.Length)])
                                       : ("Reservada", "info", "próxima hora", "—"),
                    "Ocupada"      => rnd.NextDouble() < 0.25
                                       ? ("Disponible", "active", "—", "—")
                                       : ("Ocupada",    "warn",   pick["Ocupada desde"]?.GetValue<string>() ?? "—", pick["Mesero"]?.GetValue<string>() ?? "—"),
                    "Reservada"    => rnd.NextDouble() < 0.5
                                       ? ("Ocupada", "warn", "recién llegó", Servers[rnd.Next(Servers.Length)])
                                       : ("Disponible", "active", "—", "—"),
                    _              => ("Disponible", "active", "—", "—"),
                };
                pick["Estado"]       = JsonValue.Create(newEstado);
                pick["_sc"]          = JsonValue.Create(newSc);
                pick["Ocupada desde"] = JsonValue.Create(desde);
                pick["Mesero"]       = JsonValue.Create(mesero);
                pick["_updatedAt"]   = JsonValue.Create(DateTime.UtcNow.ToString("o"));
            }
        }

        await WriteArrayAsync(Path.Combine(dataDir, file), arr);
    }

    // display.json — Cocina/KDS — columns: Orden, Mesa, Ítems, Espera, Prioridad, Estado
    private static readonly string[] KdsStatuses = ["Recibida", "En preparación", "Lista"];
    private static string KdsSc(string estado) => estado switch
    {
        "En preparación" => "warn",
        "Lista"          => "active",
        "Entregada"      => "active",
        _                => "info",
    };
    private static string KdsPriority(int waitMin) =>
        waitMin >= 20 ? "Urgente" : waitMin >= 12 ? "Alta" : "Normal";

    private static async Task AppendKitchenDisplayAsync(string dataDir, Random rnd, bool busy)
    {
        const string file = "display.json";
        var arr = await ReadArrayAsync(Path.Combine(dataDir, file));

        // Advance existing simulated tickets
        foreach (var item in arr.ToList())
        {
            if (item?["_simulated"]?.GetValue<bool>() != true) continue;
            var st  = item["Estado"]?.GetValue<string>() ?? "";
            var idx = Array.IndexOf(KdsStatuses, st);
            if (idx >= 0 && idx < KdsStatuses.Length - 1)
            {
                var next = KdsStatuses[idx + 1];
                item["Estado"] = JsonValue.Create(next);
                item["_sc"]    = JsonValue.Create(KdsSc(next));
            }
            // Increment wait time for pending items
            if (int.TryParse(item["Espera"]?.GetValue<string>()?.Replace(" min",""), out var w))
            {
                var newW = w + rnd.Next(1, 4);
                item["Espera"]    = JsonValue.Create($"{newW} min");
                item["Prioridad"] = JsonValue.Create(KdsPriority(newW));
            }
        }

        int newTickets = busy ? rnd.Next(1, 3) : 1;
        for (int i = 0; i < newTickets; i++)
        {
            var espera = rnd.Next(1, 5);
            arr.Add(new JsonObject
            {
                ["Orden"]      = $"#{rnd.Next(1200, 9999)}",
                ["Mesa"]       = $"Mesa {rnd.Next(1, 13)}",
                ["Ítems"]      = MenuItems[rnd.Next(MenuItems.Length)],
                ["Espera"]     = $"{espera} min",
                ["Prioridad"]  = KdsPriority(espera),
                ["Estado"]     = "Recibida",
                ["_sc"]        = "info",
                ["_simulated"] = true,
                ["_createdAt"] = DateTime.UtcNow.ToString("o"),
            });
        }

        TrimSimulated(arr, 30);
        await WriteArrayAsync(Path.Combine(dataDir, file), arr);
    }

    // inventario-compras.json — columns: Producto, Unidad, Stock, Mínimo, Último movimiento, Estado
    private static readonly string[] Ingredients = [
        "Arroz", "Frijoles negros", "Aceite vegetal", "Carne molida",
        "Papas", "Pollo entero", "Plátano", "Lechuga",
        "Tomate", "Cebolla",
    ];

    private static async Task UpdateInventoryAsync(string dataDir, Random rnd)
    {
        const string file = "inventario-compras.json";
        var arr = await ReadArrayAsync(Path.Combine(dataDir, file));

        // Ensure basic inventory
        var existing = arr.Select(i => i?["Producto"]?.GetValue<string>() ?? "").ToHashSet();
        foreach (var ing in Ingredients)
        {
            if (existing.Contains(ing)) continue;
            var stock = rnd.Next(15, 80);
            var min   = rnd.Next(8, 20);
            arr.Add(new JsonObject
            {
                ["Producto"]           = ing,
                ["Unidad"]             = "kg",
                ["Stock"]              = stock.ToString(),
                ["Mínimo"]             = min.ToString(),
                ["Último movimiento"]  = "hoy",
                ["Estado"]             = stock > min ? "OK" : "Bajo stock",
                ["_sc"]                = stock > min ? "active" : "danger",
                ["_simulated"]         = true,
            });
        }

        // Decrement random items to simulate consumption
        var simItems = arr.Where(i => i?["_simulated"]?.GetValue<bool>() == true).ToList();
        int toDec = Math.Min(rnd.Next(1, 4), simItems.Count);
        for (int i = 0; i < toDec; i++)
        {
            var pick = simItems[rnd.Next(simItems.Count)];
            if (pick == null) continue;
            if (!int.TryParse(pick["Stock"]?.GetValue<string>(), out var stock)) continue;
            if (!int.TryParse(pick["Mínimo"]?.GetValue<string>(), out var min)) continue;

            var newStock = Math.Max(0, stock - rnd.Next(1, 6));
            var estado   = newStock < min ? "Bajo stock" : "OK";
            pick["Stock"]              = newStock.ToString();
            pick["Estado"]             = JsonValue.Create(estado);
            pick["_sc"]                = JsonValue.Create(newStock < min ? "danger" : "active");
            pick["Último movimiento"]  = JsonValue.Create($"hace {rnd.Next(1, 60)} min");
            pick["_updatedAt"]         = JsonValue.Create(DateTime.UtcNow.ToString("o"));
        }

        await WriteArrayAsync(Path.Combine(dataDir, file), arr);
    }

    // activity.json — dashboard feed: desc, when, status, statusColor
    private static readonly string[] ActivityTemplatesPeak = [
        "Nueva orden en {mesa} — {items}",
        "{mesa} solicitó la cuenta",
        "Orden {orden} marcada como entregada",
        "Cocina: {items} listo para {mesa}",
        "{mesa} ocupada — {mesero}",
    ];
    private static readonly string[] ActivityTemplatesNormal = [
        "Mesa liberada — {mesa}",
        "Stock bajo en {ingrediente} — revisar",
        "Nueva reservación para {mesa}",
        "Orden {orden} en preparación",
        "Inventario actualizado — {ingrediente}",
    ];

    private static async Task AppendActivityAsync(string dataDir, Random rnd, string scenario)
    {
        const string file = "activity.json";
        var arr = await ReadArrayAsync(Path.Combine(dataDir, file));

        var templates = scenario == "hora_pico" || scenario == "cocina_congestionada"
            ? ActivityTemplatesPeak
            : ActivityTemplatesNormal;

        var tmpl = templates[rnd.Next(templates.Length)]
            .Replace("{mesa}",        $"Mesa {rnd.Next(1,13)}")
            .Replace("{items}",       MenuItems[rnd.Next(MenuItems.Length)])
            .Replace("{orden}",       $"#{rnd.Next(1200, 9999)}")
            .Replace("{mesero}",      Servers[rnd.Next(Servers.Length)])
            .Replace("{ingrediente}", Ingredients[rnd.Next(Ingredients.Length)]);

        // Vary colors based on event content to add visual richness
        string status, color;
        if (tmpl.Contains("solicitó la cuenta") || tmpl.Contains("Urgente"))
            (status, color) = ("Urgente", "danger");
        else if (tmpl.Contains("cocina") || tmpl.Contains("Cocina") || tmpl.Contains("preparación"))
            (status, color) = ("Cocina", "warn");
        else if (tmpl.Contains("Entregado") || tmpl.Contains("entregada") || tmpl.Contains("liberada"))
            (status, color) = ("Completado", "active");
        else if (tmpl.Contains("bajo") || tmpl.Contains("stock"))
            (status, color) = ("Alerta", "danger");
        else
            (status, color) = (scenario == "hora_pico" ? "Activo" : "Normal", scenario == "hora_pico" ? "active" : "info");

        // Prepend (newest first)
        var entry = new JsonObject
        {
            ["desc"]        = tmpl,
            ["when"]        = DateTime.UtcNow.ToString("o"),
            ["status"]      = status,
            ["statusColor"] = color,
            ["_simulated"]  = true,
        };
        arr.Insert(0, entry);

        // Keep newest 20 entries — trim oldest unconditionally (seed entries have no _simulated flag
        // and would block a conditional trim, causing unbounded growth)
        while (arr.Count > 20)
            arr.RemoveAt(arr.Count - 1);

        await WriteArrayAsync(Path.Combine(dataDir, file), arr);
    }

    // ── JSON helpers ──────────────────────────────────────────────────────────

    private static async Task<JsonArray> ReadArrayAsync(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            var txt = await File.ReadAllTextAsync(path, System.Text.Encoding.UTF8);
            return JsonNode.Parse(txt)?.AsArray() ?? [];
        }
        catch { return []; }
    }

    private static async Task WriteArrayAsync(string path, JsonArray arr)
    {
        var txt = arr.ToJsonString(WriteOpts);
        await File.WriteAllTextAsync(path, txt, Utf8NoBom);
    }

    private static void TrimSimulated(JsonArray arr, int maxSimulated)
    {
        var simulated = arr
            .Where(n => n?["_simulated"]?.GetValue<bool>() == true)
            .ToList();

        while (simulated.Count > maxSimulated)
        {
            var oldest = simulated[0];
            simulated.RemoveAt(0);
            arr.Remove(oldest);
        }
    }
}
