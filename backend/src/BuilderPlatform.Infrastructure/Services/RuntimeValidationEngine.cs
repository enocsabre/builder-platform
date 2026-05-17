using BuilderPlatform.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BuilderPlatform.Infrastructure.Services;

public record GateResult(
    string  Gate,
    string  Category,
    bool    Passed,
    bool    Skipped,
    string  Message,
    string? Detail
);

public class RuntimeValidationEngine(ILogger<RuntimeValidationEngine> logger, PreviewRunner previewRunner)
{
    private const string CatRegistry = "registry";
    private const string CatRuntime  = "runtime";
    private const string CatBuild    = "build";

    // ── Public entry point ─────────────────────────────────────────────────────

    public async Task<(List<GateResult> Gates, string Logs, string? Errors)> RunAllGatesAsync(
        Product product, CancellationToken ct)
    {
        var gates  = new List<GateResult>();
        var log    = new StringBuilder();
        var errors = new StringBuilder();

        // ── Registry gates (always run, fast) ─────────────────────────────────
        gates.Add(await CheckRegistryJsonGate("registry_nav",       CatRegistry, product, "frontend/registry/nav-items.json", ct));
        gates.Add(await CheckRegistryJsonGate("registry_dashboard", CatRegistry, product, "frontend/registry/dashboard.json", ct));
        gates.Add(await CheckRegistryJsonGate("registry_modules",   CatRegistry, product, "frontend/registry/modules.json",   ct));

        // ── Runtime gates ──────────────────────────────────────────────────────
        var (liveStatus, _, livePort) = previewRunner.GetLiveStatus(product.Id);
        var previewRunning = liveStatus == "running" && livePort.HasValue;

        // Fallback: if in-memory state was lost on restart, probe the stored PreviewUrl directly
        string? dashboardUrl = null;
        if (!previewRunning && !string.IsNullOrWhiteSpace(product.PreviewUrl))
        {
            var probe = await CheckHttpGate("preview_running", CatRuntime, product.PreviewUrl, ct);
            if (probe.Passed)
            {
                previewRunning = true;
                dashboardUrl   = product.PreviewUrl.TrimEnd('/') + "/dashboard";
                gates.Add(new GateResult("preview_running", CatRuntime, true, false, $"Preview activo (HTTP) en {product.PreviewUrl}", null));
            }
            else
            {
                gates.Add(new GateResult("preview_running", CatRuntime, false, false, $"Preview no activo (status: {liveStatus})", null));
            }
        }
        else
        {
            gates.Add(previewRunning
                ? new GateResult("preview_running", CatRuntime, true,  false, $"Preview activo en puerto {livePort}", null)
                : new GateResult("preview_running", CatRuntime, false, false, $"Preview no activo (status: {liveStatus})", null));
            if (previewRunning && livePort.HasValue)
                dashboardUrl = $"http://localhost:{livePort}/dashboard";
        }

        if (previewRunning && dashboardUrl != null)
            gates.Add(await CheckHttpGate("dashboard_route", CatRuntime, dashboardUrl, ct));

        // ── Build gates (slower, skip if no project) ───────────────────────────
        if (string.IsNullOrWhiteSpace(product.ProjectPath))
        {
            gates.Add(new GateResult("frontend_typecheck", CatBuild, false, true, "Sin projectPath — scaffold incompleto", null));
            gates.Add(new GateResult("backend_build",      CatBuild, false, true, "Sin projectPath — scaffold incompleto", null));
        }
        else
        {
            var frontendDir = Path.Combine(product.ProjectPath, "frontend");
            var backendDir  = Path.Combine(product.ProjectPath, "backend");

            gates.Add(Directory.Exists(frontendDir)
                ? await CheckProcessGate("frontend_typecheck", CatBuild, "cmd.exe", "/c npx tsc --noEmit", frontendDir, 45_000, ct)
                : new GateResult("frontend_typecheck", CatBuild, false, true, "Directorio frontend no encontrado", null));

            gates.Add(Directory.Exists(backendDir)
                ? await CheckProcessGate("backend_build", CatBuild, "dotnet", "build --nologo -v quiet", backendDir, 90_000, ct)
                : new GateResult("backend_build", CatBuild, false, true, "Directorio backend no encontrado", null));
        }

        // ── Build summary log ──────────────────────────────────────────────────
        foreach (var g in gates)
        {
            var status = g.Skipped ? "SKIP" : g.Passed ? "PASS" : "FAIL";
            log.AppendLine($"[{status}] {g.Category}/{g.Gate}: {g.Message}");
            if (!g.Passed && !g.Skipped && g.Detail != null)
                errors.AppendLine($"--- {g.Gate} ---\n{g.Detail[..Math.Min(g.Detail.Length, 400)]}");
        }

        return (gates, log.ToString(), errors.Length > 0 ? errors.ToString() : null);
    }

    // ── Gate implementations ───────────────────────────────────────────────────

    private async Task<GateResult> CheckRegistryJsonGate(
        string gateName, string category, Product product, string relPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(product.ProjectPath))
            return new GateResult(gateName, category, false, true, "Sin projectPath", null);

        var fullPath = Path.Combine(product.ProjectPath, relPath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
            return new GateResult(gateName, category, false, false, $"Archivo no encontrado: {relPath}", null);

        try
        {
            var content = await File.ReadAllTextAsync(fullPath, ct);
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var count = doc.RootElement.GetArrayLength();
                return count == 0
                    ? new GateResult(gateName, category, false, false, $"{relPath} es un array vacío", null)
                    : new GateResult(gateName, category, true,  false, $"Array válido — {count} item(s)", null);
            }

            return new GateResult(gateName, category, true, false, "JSON válido (objeto)", null);
        }
        catch (JsonException ex)
        {
            var msg = ex.Message.Length > 100 ? ex.Message[..100] : ex.Message;
            return new GateResult(gateName, category, false, false, $"JSON malformado: {msg}", ex.Message);
        }
    }

    private async Task<GateResult> CheckHttpGate(string gateName, string category, string url, CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var resp = await http.GetAsync(url, ct);
            var code = (int)resp.StatusCode;
            var ok   = code is >= 200 and < 400;
            return new GateResult(gateName, category, ok, false,
                ok ? $"HTTP {code} OK" : $"HTTP {code} — se esperaba 2xx",
                ok ? null : $"{url} devolvió {code}");
        }
        catch (Exception ex)
        {
            var msg = ex.Message.Length > 80 ? ex.Message[..80] : ex.Message;
            return new GateResult(gateName, category, false, false, $"Conexión fallida: {msg}", ex.Message);
        }
    }

    private async Task<GateResult> CheckProcessGate(
        string gateName, string category,
        string exe, string args, string workingDir, int timeoutMs, CancellationToken ct)
    {
        try
        {
            var (exitCode, stdout, stderr) = await RunCommandAsync(exe, args, workingDir, timeoutMs, ct);
            var combined = (stdout + stderr).Trim();
            var detail   = combined.Length > 800 ? combined[..800] + "\n…[truncado]" : combined;

            if (exitCode == -1)
                return new GateResult(gateName, category, false, false, $"Timeout ({timeoutMs / 1000}s excedido)", detail.Length > 0 ? detail : null);

            return exitCode == 0
                ? new GateResult(gateName, category, true,  false, "Build limpio — 0 errores", null)
                : new GateResult(gateName, category, false, false, $"Salió con código {exitCode}", detail.Length > 0 ? detail : null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gate {Gate} process error", gateName);
            return new GateResult(gateName, category, false, false, $"Error al ejecutar: {ex.Message[..Math.Min(ex.Message.Length, 80)]}", null);
        }
    }

    // ── Process runner ─────────────────────────────────────────────────────────

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCommandAsync(
        string exe, string args, string workingDir, int timeoutMs, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = exe,
            Arguments              = args,
            WorkingDirectory       = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return (-1, stdout.ToString(), stderr.ToString());
        }

        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
