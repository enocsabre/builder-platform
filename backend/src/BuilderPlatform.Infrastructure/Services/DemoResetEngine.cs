using System.Text;

namespace BuilderPlatform.Infrastructure.Services;

/// <summary>
/// Writes a canonical, coherent demo seed to a product's .data/ directory.
/// Represents a typical lunch-service restaurant at ~14:40 with believable KPIs.
/// </summary>
public class DemoResetEngine
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public async Task ResetAsync(string projectPath, CancellationToken ct = default)
    {
        var dataDir = Path.Combine(projectPath, "frontend", ".data");
        Directory.CreateDirectory(dataDir);

        await File.WriteAllTextAsync(Path.Combine(dataDir, "mesas.json"),             MesasJson,      Utf8NoBom, ct);
        await File.WriteAllTextAsync(Path.Combine(dataDir, "pedidos-comandas.json"),   PedidosJson,    Utf8NoBom, ct);
        await File.WriteAllTextAsync(Path.Combine(dataDir, "display.json"),            DisplayJson,    Utf8NoBom, ct);
        await File.WriteAllTextAsync(Path.Combine(dataDir, "inventario-compras.json"), InventarioJson, Utf8NoBom, ct);
        await File.WriteAllTextAsync(Path.Combine(dataDir, "activity.json"),           ActivityJson,   Utf8NoBom, ct);
    }

    // ── Canonical seed data — 14:40 lunch service ─────────────────────────────
    // Raw JSON so keys can use Spanish names with accents/spaces exactly as the
    // frontend pages expect (C# anonymous objects cannot have such identifiers).

    private const string MesasJson = """
        [
          { "Mesa": "Mesa 1",  "Capacidad": "4 pers.", "Estado": "Ocupada",    "Ocupada desde": "hace 35 min", "Mesero": "Ana G.",     "_sc": "warn"   },
          { "Mesa": "Mesa 2",  "Capacidad": "6 pers.", "Estado": "Ocupada",    "Ocupada desde": "hace 52 min", "Mesero": "Luis R.",    "_sc": "warn"   },
          { "Mesa": "Mesa 3",  "Capacidad": "2 pers.", "Estado": "Disponible", "Ocupada desde": "—",           "Mesero": "—",         "_sc": "active" },
          { "Mesa": "Mesa 4",  "Capacidad": "4 pers.", "Estado": "Reservada",  "Ocupada desde": "próxima hora","Mesero": "—",         "_sc": "info"   },
          { "Mesa": "Mesa 5",  "Capacidad": "2 pers.", "Estado": "Ocupada",    "Ocupada desde": "hace 8 min",  "Mesero": "María C.",  "_sc": "warn"   },
          { "Mesa": "Mesa 6",  "Capacidad": "6 pers.", "Estado": "Ocupada",    "Ocupada desde": "hace 21 min", "Mesero": "Carlos A.", "_sc": "warn"   },
          { "Mesa": "Mesa 7",  "Capacidad": "2 pers.", "Estado": "Disponible", "Ocupada desde": "—",           "Mesero": "—",         "_sc": "active" },
          { "Mesa": "Mesa 8",  "Capacidad": "4 pers.", "Estado": "Reservada",  "Ocupada desde": "próxima hora","Mesero": "—",         "_sc": "info"   },
          { "Mesa": "Mesa 9",  "Capacidad": "6 pers.", "Estado": "Ocupada",    "Ocupada desde": "hace 17 min", "Mesero": "Sofía M.",  "_sc": "warn"   },
          { "Mesa": "Mesa 10", "Capacidad": "4 pers.", "Estado": "Disponible", "Ocupada desde": "—",           "Mesero": "—",         "_sc": "active" },
          { "Mesa": "Mesa 11", "Capacidad": "2 pers.", "Estado": "Ocupada",    "Ocupada desde": "hace 4 min",  "Mesero": "Ana G.",    "_sc": "warn"   },
          { "Mesa": "Mesa 12", "Capacidad": "6 pers.", "Estado": "Ocupada",    "Ocupada desde": "hace 29 min", "Mesero": "Luis R.",   "_sc": "warn"   }
        ]
        """;

    private const string PedidosJson = """
        [
          { "Orden": "#1241", "Mesa": "Mesa 2",  "Ítems": "Casado con pollo x2, Agua mineral x2",  "Total": "₡19,200", "Hora": "13:52", "Estado": "Entregado",  "_sc": "active" },
          { "Orden": "#1242", "Mesa": "Mesa 1",  "Ítems": "Sopa negra x2, Refresco x2",            "Total": "₡13,600", "Hora": "14:05", "Estado": "Entregado",  "_sc": "active" },
          { "Orden": "#1243", "Mesa": "Mesa 12", "Ítems": "Gallo pinto x3, Café chorreado x1",     "Total": "₡14,800", "Hora": "14:11", "Estado": "Entregado",  "_sc": "active" },
          { "Orden": "#1244", "Mesa": "Mesa 6",  "Ítems": "Chifrijo x2, Limonada natural x2",      "Total": "₡16,400", "Hora": "14:19", "Estado": "En camino",  "_sc": "info"   },
          { "Orden": "#1245", "Mesa": "Mesa 9",  "Ítems": "Arroz con leche x1, Ensalada x1",       "Total": "₡11,300", "Hora": "14:23", "Estado": "En camino",  "_sc": "info"   },
          { "Orden": "#1246", "Mesa": "Mesa 5",  "Ítems": "Patacones x2, Agua mineral",            "Total": "₡9,800",  "Hora": "14:32", "Estado": "Preparando", "_sc": "warn"   },
          { "Orden": "#1247", "Mesa": "Mesa 1",  "Ítems": "Casado con pollo x1, Café chorreado",   "Total": "₡11,700", "Hora": "14:36", "Estado": "Preparando", "_sc": "warn"   },
          { "Orden": "#1248", "Mesa": "Mesa 11", "Ítems": "Gallo pinto x1",                        "Total": "₡5,800",  "Hora": "14:41", "Estado": "Pendiente",  "_sc": "info"   },
          { "Orden": "#1249", "Mesa": "Mesa 2",  "Ítems": "Sopa negra x2, Patacones x1",           "Total": "₡14,200", "Hora": "14:43", "Estado": "Pendiente",  "_sc": "info"   }
        ]
        """;

    private const string DisplayJson = """
        [
          { "Orden": "#1246", "Mesa": "Mesa 5",  "Ítems": "Patacones x2",              "Espera": "8 min", "Prioridad": "Normal", "Estado": "En preparación", "_sc": "warn" },
          { "Orden": "#1247", "Mesa": "Mesa 1",  "Ítems": "Casado con pollo x1",       "Espera": "4 min", "Prioridad": "Normal", "Estado": "En preparación", "_sc": "warn" },
          { "Orden": "#1248", "Mesa": "Mesa 11", "Ítems": "Gallo pinto x1",            "Espera": "2 min", "Prioridad": "Normal", "Estado": "Recibida",        "_sc": "info" },
          { "Orden": "#1249", "Mesa": "Mesa 2",  "Ítems": "Sopa negra x2, Patacones", "Espera": "1 min", "Prioridad": "Normal", "Estado": "Recibida",        "_sc": "info" }
        ]
        """;

    private const string InventarioJson = """
        [
          { "Producto": "Arroz",           "Unidad": "kg",     "Stock": "4",  "Mínimo": "8",  "Último movimiento": "hace 12 min", "Estado": "Bajo stock", "_sc": "danger" },
          { "Producto": "Frijoles negros", "Unidad": "kg",     "Stock": "22", "Mínimo": "8",  "Último movimiento": "hoy",         "Estado": "OK",         "_sc": "active" },
          { "Producto": "Aceite vegetal",  "Unidad": "litros", "Stock": "6",  "Mínimo": "10", "Último movimiento": "hace 45 min", "Estado": "Bajo stock", "_sc": "danger" },
          { "Producto": "Carne molida",    "Unidad": "kg",     "Stock": "18", "Mínimo": "8",  "Último movimiento": "hoy",         "Estado": "OK",         "_sc": "active" },
          { "Producto": "Papas",           "Unidad": "kg",     "Stock": "31", "Mínimo": "10", "Último movimiento": "hoy",         "Estado": "OK",         "_sc": "active" },
          { "Producto": "Pollo entero",    "Unidad": "kg",     "Stock": "14", "Mínimo": "8",  "Último movimiento": "hoy",         "Estado": "OK",         "_sc": "active" },
          { "Producto": "Plátano",         "Unidad": "kg",     "Stock": "19", "Mínimo": "5",  "Último movimiento": "hoy",         "Estado": "OK",         "_sc": "active" },
          { "Producto": "Lechuga",         "Unidad": "kg",     "Stock": "3",  "Mínimo": "5",  "Último movimiento": "hace 1 hora", "Estado": "Bajo stock", "_sc": "danger" },
          { "Producto": "Tomate",          "Unidad": "kg",     "Stock": "9",  "Mínimo": "5",  "Último movimiento": "hoy",         "Estado": "OK",         "_sc": "active" },
          { "Producto": "Cebolla",         "Unidad": "kg",     "Stock": "14", "Mínimo": "5",  "Último movimiento": "hoy",         "Estado": "OK",         "_sc": "active" }
        ]
        """;

    private const string ActivityJson = """
        [
          { "desc": "Mesa 11 ocupada — Ana G.",                     "when": "hace 4 min",  "status": "Activo",     "statusColor": "active" },
          { "desc": "Nueva orden #1249 en Mesa 2 — Sopa negra",     "when": "hace 6 min",  "status": "Nuevo",      "statusColor": "info"   },
          { "desc": "Orden #1248 recibida en cocina",                "when": "hace 7 min",  "status": "Cocina",     "statusColor": "warn"   },
          { "desc": "Stock bajo en Arroz — revisar inventario",      "when": "hace 12 min", "status": "Alerta",     "statusColor": "danger" },
          { "desc": "Orden #1247 en preparación — Casado con pollo", "when": "hace 14 min", "status": "Cocina",     "statusColor": "warn"   },
          { "desc": "Mesa 5 ocupada — María C.",                     "when": "hace 18 min", "status": "Activo",     "statusColor": "active" },
          { "desc": "Mesa 9 ocupada — Sofía M.",                     "when": "hace 22 min", "status": "Activo",     "statusColor": "active" },
          { "desc": "Stock bajo en Aceite vegetal",                   "when": "hace 34 min", "status": "Alerta",     "statusColor": "danger" },
          { "desc": "Orden #1243 entregada — Mesa 12",               "when": "hace 39 min", "status": "Completado", "statusColor": "active" },
          { "desc": "Orden #1242 marcada como entregada — Mesa 1",   "when": "hace 46 min", "status": "Completado", "statusColor": "active" },
          { "desc": "Reservación confirmada para Mesa 4",            "when": "hace 52 min", "status": "Reserva",    "statusColor": "info"   },
          { "desc": "Apertura del servicio — mesas activas",         "when": "hace 1 hora", "status": "Sistema",    "statusColor": "info"   }
        ]
        """;
}
