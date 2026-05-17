using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using DomainActivity = BuilderPlatform.Domain.Entities.ActivityEvent;

namespace BuilderPlatform.Infrastructure.Services;

public class PreviewRunner(IServiceScopeFactory scopeFactory, ILogger<PreviewRunner> logger)
{
    private readonly record struct RunningPreview(Process Process, int Port, DateTime StartedAt);
    private readonly ConcurrentDictionary<Guid, RunningPreview> _running = new();

    private const int PortRangeStart = 3100;
    private const int PortRangeEnd   = 3200;

    // ── Public API ─────────────────────────────────────────────────────────────

    public void StartAsync(Guid productId)
        => Task.Run(() => StartInternalAsync(productId));

    public async Task StopAsync(Guid productId)
    {
        if (_running.TryRemove(productId, out var preview))
        {
            try { KillTree(preview.Process.Id); } catch (Exception ex) { logger.LogWarning(ex, "Kill failed for pid {Pid}", preview.Process.Id); }
        }
        await UpdateStateAsync(productId, "stopped", null, null, DateTime.UtcNow, null, ActivityType.PreviewStopped, "Preview detenido");
    }

    public (string status, string? url, int? port) GetLiveStatus(Guid productId)
    {
        if (!_running.TryGetValue(productId, out var preview))
            return ("stopped", null, null);
        if (preview.Process.HasExited)
        {
            _running.TryRemove(productId, out _);
            return ("stopped", null, null);
        }
        return ("running", $"http://localhost:{preview.Port}", preview.Port);
    }


    // ── Internal pipeline ──────────────────────────────────────────────────────

    private async Task StartInternalAsync(Guid productId)
    {
        try
        {
            // Already running?
            if (_running.TryGetValue(productId, out var existing) && !existing.Process.HasExited)
            {
                await UpdateStateAsync(productId, "running",
                    $"http://localhost:{existing.Port}", existing.Port, existing.StartedAt, null,
                    ActivityType.PreviewRunning, $"Preview ya activo en puerto {existing.Port}");
                return;
            }

            string? frontendPath = await GetFrontendPathAsync(productId);
            if (frontendPath is null)
            { await SetErrorAsync(productId, "Producto sin ProjectPath — generá el scaffold primero"); return; }

            if (!Directory.Exists(frontendPath))
            { await SetErrorAsync(productId, $"Directorio frontend no encontrado: {frontendPath}"); return; }

            // Mark starting
            await UpdateStateAsync(productId, "starting", null, null, DateTime.UtcNow, null,
                ActivityType.PreviewStarting, "Iniciando preview del SaaS generado...");

            // npm install if node_modules missing
            if (!Directory.Exists(Path.Combine(frontendPath, "node_modules")))
            {
                logger.LogInformation("Running npm install for {ProductId} at {Path}", productId, frontendPath);
                await AddActivityAsync(productId, ActivityType.PreviewStarting, "Instalando dependencias (npm install)...",
                    "Primera vez — puede tardar 60-120s");

                int exitCode = await RunCommandAsync("npm install", frontendPath, 180_000);
                if (exitCode != 0)
                { await SetErrorAsync(productId, "npm install falló. Verificá que Node.js esté instalado."); return; }
            }

            // Find free port
            int port = FindFreePort();
            if (port == -1)
            { await SetErrorAsync(productId, $"No hay puertos disponibles en el rango {PortRangeStart}-{PortRangeEnd}"); return; }

            logger.LogInformation("Starting preview for {ProductId} on port {Port}", productId, port);

            // Start npm run dev
            var proc = CreateProcess("npm run dev -- --port " + port, frontendPath);

            var outputBuf = new StringBuilder();
            var readyTcs  = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                outputBuf.AppendLine(e.Data);
                logger.LogDebug("[preview:{Id}] {Line}", productId, e.Data);
                if (e.Data.Contains("Ready",  StringComparison.OrdinalIgnoreCase) ||
                    e.Data.Contains("Local:",  StringComparison.OrdinalIgnoreCase) ||
                    e.Data.Contains("started", StringComparison.OrdinalIgnoreCase))
                    readyTcs.TrySetResult(true);
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                outputBuf.AppendLine("[ERR] " + e.Data);
                if (e.Data.Contains("EADDRINUSE", StringComparison.OrdinalIgnoreCase))
                    readyTcs.TrySetResult(false);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            _running[productId] = new RunningPreview(proc, port, DateTime.UtcNow);

            // Wait up to 120s for ready signal
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var winner = await Task.WhenAny(readyTcs.Task, proc.WaitForExitAsync(cts.Token));

            if (proc.HasExited)
            {
                _running.TryRemove(productId, out _);
                var tail = outputBuf.Length > 400 ? outputBuf.ToString()[^400..] : outputBuf.ToString();
                await SetErrorAsync(productId, $"Proceso terminó inesperadamente. Salida:\n{tail.Trim()}");
                return;
            }

            // Running (or optimistic timeout)
            var url = $"http://localhost:{port}";
            await UpdateStateAsync(productId, "running", url, port, DateTime.UtcNow, null,
                ActivityType.PreviewRunning, $"Preview activo en {url}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Preview start failed for {ProductId}", productId);
            await SetErrorAsync(productId, ex.Message);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Process CreateProcess(string command, string workDir)
    {
        var proc = new Process();
        proc.StartInfo.FileName               = "cmd.exe";
        proc.StartInfo.Arguments              = "/c " + command;
        proc.StartInfo.WorkingDirectory       = workDir;
        proc.StartInfo.UseShellExecute        = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError  = true;
        proc.StartInfo.CreateNoWindow         = true;
        return proc;
    }

    private static async Task<int> RunCommandAsync(string command, string workDir, int timeoutMs)
    {
        var proc = CreateProcess(command, workDir);
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        using var cts = new CancellationTokenSource(timeoutMs);
        try { await proc.WaitForExitAsync(cts.Token); return proc.ExitCode; }
        catch (OperationCanceledException) { try { proc.Kill(true); } catch { } return -1; }
    }

    private static int FindFreePort()
    {
        for (int port = PortRangeStart; port <= PortRangeEnd; port++)
        {
            try
            {
                using var listener = new TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch { /* in use */ }
        }
        return -1;
    }

    private static void KillTree(int pid)
    {
        var kill = Process.Start(new ProcessStartInfo("taskkill")
        {
            Arguments             = $"/F /T /PID {pid}",
            UseShellExecute       = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow        = true,
        });
        kill?.WaitForExit(5000);
    }

    // ── DB helpers ─────────────────────────────────────────────────────────────

    private async Task<string?> GetFrontendPathAsync(Guid productId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId);
        if (product?.ProjectPath is null) return null;
        return Path.Combine(product.ProjectPath, "frontend");
    }

    private async Task SetErrorAsync(Guid productId, string error)
    {
        logger.LogWarning("Preview error for {ProductId}: {Error}", productId, error);
        await UpdateStateAsync(productId, "error", null, null, null, error,
            ActivityType.PreviewError, "Error al iniciar preview", error);
    }

    private async Task UpdateStateAsync(Guid productId, string status, string? url, int? port,
        DateTime? startedAt, string? error, ActivityType activityType, string activityTitle,
        string? activityDetails = null)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var product = await db.Products.FindAsync(productId);
        if (product is null) return;

        product.PreviewStatus = status;
        product.PreviewUrl    = url;
        if (port.HasValue)        product.PreviewPort = port;
        if (startedAt.HasValue)   product.PreviewLastStartedAt = startedAt;
        product.PreviewError  = error;
        product.UpdatedAt     = DateTime.UtcNow;

        db.ActivityEvents.Add(new DomainActivity
        {
            ProductId = productId,
            EventType = activityType,
            Title     = activityTitle,
            Details   = activityDetails,
        });

        await db.SaveChangesAsync();
    }

    private async Task AddActivityAsync(Guid productId, ActivityType type, string title, string? details = null)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ActivityEvents.Add(new DomainActivity { ProductId = productId, EventType = type, Title = title, Details = details });
        await db.SaveChangesAsync();
    }
}
