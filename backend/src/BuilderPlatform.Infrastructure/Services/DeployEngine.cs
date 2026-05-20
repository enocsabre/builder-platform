using BuilderPlatform.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace BuilderPlatform.Infrastructure.Services;

public class DeployEngine(
    ILogger<DeployEngine> logger,
    RuntimeValidationEngine validationEngine,
    IConfiguration configuration)
{
    // ── Pre-deploy validation ──────────────────────────────────────────────────

    public async Task<(List<GateResult> Gates, bool AllPassed, string Logs)> RunPreDeployGatesAsync(
        Product product, CancellationToken ct)
    {
        var (gates, logs, _) = await validationEngine.RunAllGatesAsync(product, ct);

        // Runtime gates (preview_running, dashboard_route) check local dev server state.
        // For a production deploy to Vercel, the local preview is irrelevant — skip them.
        var runtimeLocalGates = new HashSet<string>(["preview_running", "dashboard_route"]);
        for (var i = 0; i < gates.Count; i++)
        {
            if (runtimeLocalGates.Contains(gates[i].Gate) && !gates[i].Passed)
                gates[i] = gates[i] with { Passed = false, Skipped = true, Message = "Skipped for deploy — estado del dev server local no es requisito de producción" };
        }

        // Additional gate: next build (slow — only for deploy path)
        var nextBuildGate = await RunNextBuildGateAsync(product, ct);
        gates.Add(nextBuildGate);

        var logBuilder = new StringBuilder(logs);
        var status     = nextBuildGate.Skipped ? "SKIP" : nextBuildGate.Passed ? "PASS" : "FAIL";
        logBuilder.AppendLine($"[{status}] build/next_build: {nextBuildGate.Message}");

        var allPassed = gates.All(g => g.Passed || g.Skipped);
        return (gates, allPassed, logBuilder.ToString());
    }

    private async Task<GateResult> RunNextBuildGateAsync(Product product, CancellationToken ct)
    {
        const string gate     = "next_build";
        const string category = "build";

        if (string.IsNullOrWhiteSpace(product.ProjectPath))
            return new GateResult(gate, category, false, true, "Sin projectPath — scaffold incompleto", null);

        var frontendDir = Path.Combine(product.ProjectPath, "frontend");
        if (!Directory.Exists(frontendDir))
            return new GateResult(gate, category, false, true, "Directorio frontend no encontrado", null);

        var (exitCode, stdout, stderr) = await RunCommandAsync(
            "cmd.exe", "/c npx next build", frontendDir, 300_000, ct);

        var combined = (stdout + stderr).Trim();
        var detail   = combined.Length > 1000 ? combined[..1000] + "\n…[truncado]" : combined;

        if (exitCode == -1)
            return new GateResult(gate, category, false, false, "Timeout (300s excedido)", detail.Length > 0 ? detail : null);

        return exitCode == 0
            ? new GateResult(gate, category, true,  false, "next build exitoso — app lista para deploy", null)
            : new GateResult(gate, category, false, false, $"next build falló (exit {exitCode})", detail.Length > 0 ? detail : null);
    }

    // ── Git awareness ──────────────────────────────────────────────────────────

    public async Task<(string? CommitHash, string? Branch)> GetGitInfoAsync(
        string projectPath, CancellationToken ct)
    {
        try
        {
            var repoRoot = FindGitRoot(projectPath);
            if (repoRoot is null) return (null, null);

            var (_, hashOut, _)   = await RunCommandAsync("git", "rev-parse --short HEAD", repoRoot, 5_000, ct);
            var (_, branchOut, _) = await RunCommandAsync("git", "rev-parse --abbrev-ref HEAD", repoRoot, 5_000, ct);
            return (hashOut.Trim().NullIfEmpty(), branchOut.Trim().NullIfEmpty());
        }
        catch { return (null, null); }
    }

    private static string? FindGitRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    // ── Execute deploy ─────────────────────────────────────────────────────────

    public async Task<(bool Success, string? DeployUrl, string Logs)> ExecuteDeployAsync(
        Product product, CancellationToken ct)
    {
        var provider = configuration["Deploy:Provider"] ?? "none";
        logger.LogInformation("Deploy: provider={Provider} product={Id}", provider, product.Id);

        return provider.ToLowerInvariant() switch
        {
            "vercel" => await DeployVercelAsync(product, ct),
            "swa"    => (false, null,
                "Azure Static Web Apps: configura Deploy:SwaToken y Deploy:SwaAppName en appsettings.Development.json"),
            _        => (false, null,
                "No hay deploy provider configurado. Agrega Deploy:Provider=vercel en appsettings.Development.json"),
        };
    }

    private async Task<(bool, string?, string)> DeployVercelAsync(Product product, CancellationToken ct)
    {
        var token       = configuration["Deploy:VercelToken"];
        var frontendDir = Path.Combine(product.ProjectPath!, "frontend");

        if (!Directory.Exists(frontendDir))
            return (false, null, $"Directorio frontend no encontrado: {frontendDir}");

        var tokenArg = string.IsNullOrWhiteSpace(token) ? "" : $" --token {token}";

        // Switch to configured scope before deploying (personal account: username slug; team: team slug).
        // Vercel CLI v36+ does not support --scope for personal accounts — use `vercel teams switch` instead.
        var scope = configuration["Deploy:VercelScope"] ?? "";
        if (!string.IsNullOrWhiteSpace(scope))
        {
            logger.LogInformation("Switching Vercel scope to {Scope}", scope);
            await RunCommandAsync("cmd.exe", $"/c npx vercel teams switch {scope} --yes", frontendDir, 15_000, ct);
        }

        var args = $"/c npx vercel --prod --yes{tokenArg}";

        logger.LogInformation("Deploy Vercel: running in {Dir}", frontendDir);
        var (exitCode, stdout, stderr) = await RunCommandAsync("cmd.exe", args, frontendDir, 300_000, ct);

        var combined = (stdout + "\n" + stderr).Trim();
        var logs     = combined.Length > 4000 ? combined[..4000] + "\n…[truncado]" : combined;

        if (exitCode == -1)
            return (false, null, $"Timeout (300s) — deploy cancelado.\n{logs}");

        if (exitCode != 0)
        {
            logger.LogWarning("Vercel deploy failed exit={Code}", exitCode);
            return (false, null, logs);
        }

        // Prefer the Aliased URL (shorter, no team name in domain — publicly accessible without Deployment Protection).
        // Fall back to Production URL, then any vercel.app URL.
        var aliasMatch = Regex.Match(combined, @"Aliased\s+(https://[^\s]+)", RegexOptions.Multiline);
        string? deployUrl = aliasMatch.Success ? aliasMatch.Groups[1].Value.Trim() : null;

        if (deployUrl is null)
        {
            var prodMatch = Regex.Match(combined, @"(?:▲ Production|Production:)\s+(https://[^\s]+)", RegexOptions.Multiline);
            if (prodMatch.Success) deployUrl = prodMatch.Groups[1].Value.Trim();
        }

        if (deployUrl is null)
        {
            var urlMatch = Regex.Match(combined, @"https://[a-zA-Z0-9\-]+\.vercel\.app", RegexOptions.Multiline);
            if (urlMatch.Success) deployUrl = urlMatch.Value;
        }

        logger.LogInformation("Vercel deploy succeeded: {Url}", deployUrl);
        return (true, deployUrl, logs);
    }

    // ── Shared process runner ──────────────────────────────────────────────────

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

file static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
