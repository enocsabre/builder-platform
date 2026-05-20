using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DomainActivity = BuilderPlatform.Domain.Entities.ActivityEvent;

namespace BuilderPlatform.Infrastructure.Services;

public class PreviewRunner(IServiceScopeFactory scopeFactory, ILogger<PreviewRunner> logger, RuntimeEventBus bus)
{
    private readonly record struct RunningPreview(Process Process, int Port, DateTime StartedAt);
    private readonly ConcurrentDictionary<Guid, RunningPreview> _running = new();

    private const int PortRangeStart = 3100;
    private const int PortRangeEnd   = 3200;

    // ── PID registry — tracks Builder-owned preview processes across backend restarts ───────
    private static readonly string PidRegistryPath =
        Path.Combine(Path.GetTempPath(), "bp-preview-pids.json");
    private static readonly SemaphoreSlim _registryLock = new(1, 1);
    private record PidEntry(Guid ProductId, int Pid, int Port, string FrontendPath, DateTime StartedAt);

    // ── Public API ─────────────────────────────────────────────────────────────

    public void StartAsync(Guid productId)
        => Task.Run(() => StartInternalAsync(productId, isRestart: false));

    public async Task StopAsync(Guid productId)
    {
        if (_running.TryRemove(productId, out var preview))
        {
            try { KillTree(preview.Process.Id); } catch (Exception ex) { logger.LogWarning(ex, "Kill failed for pid {Pid}", preview.Process.Id); }
            await UnregisterPidAsync(productId);
        }
        await UpdateStateAsync(productId, "stopped", null, null, DateTime.UtcNow, null, ActivityType.PreviewStopped, "Preview detenido");
    }

    // ── Startup cleanup — call once from RuntimeOrchestrator.StartAsync ───────────────────

    public async Task StartupCleanupAsync()
    {
        var killed  = 0;
        var skipped = 0;

        // Step 1: Kill known Builder Preview processes from previous backend session
        if (File.Exists(PidRegistryPath))
        {
            List<PidEntry> entries = [];
            try
            {
                var json = await File.ReadAllTextAsync(PidRegistryPath);
                entries = JsonSerializer.Deserialize<List<PidEntry>>(json) ?? [];
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Preview startup: failed to read PID registry at {Path}", PidRegistryPath);
            }

            foreach (var entry in entries)
            {
                try
                {
                    var proc = Process.GetProcessById(entry.Pid);
                    if (proc.HasExited) { logger.LogDebug("Preview startup: PID {Pid} already exited", entry.Pid); continue; }

                    // Safety check: only kill node/cmd/npm processes — never kill unrelated PIDs
                    var name = proc.ProcessName.ToLowerInvariant();
                    if (name != "node" && name != "cmd" && name != "npm")
                    {
                        logger.LogWarning(
                            "Preview startup: PID {Pid} is '{Name}' (not node/cmd/npm) — SKIPPING kill to protect external process",
                            entry.Pid, proc.ProcessName);
                        skipped++;
                        continue;
                    }

                    logger.LogInformation(
                        "Preview startup: killing Builder Preview PID {Pid} ({Name}) — product {ProductId} port {Port}",
                        entry.Pid, proc.ProcessName, entry.ProductId, entry.Port);
                    KillTree(entry.Pid);
                    killed++;
                }
                catch (ArgumentException)
                {
                    // Process already gone — PID no longer valid
                    logger.LogDebug("Preview startup: PID {Pid} not found (already gone)", entry.Pid);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Preview startup: failed to kill PID {Pid} for product {ProductId}", entry.Pid, entry.ProductId);
                    skipped++;
                }
            }

            try { File.Delete(PidRegistryPath); }
            catch (Exception ex) { logger.LogWarning(ex, "Preview startup: failed to delete PID registry"); }

            if (entries.Count > 0)
                logger.LogInformation(
                    "Preview startup: PID registry cleared — {Killed} killed, {Skipped} skipped (of {Total})",
                    killed, skipped, entries.Count);
        }

        // Step 2: Fix DB state — any product still marked running/starting has no live process now
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var staleProducts = await db.Products
            .Where(p => p.PreviewStatus == "running" || p.PreviewStatus == "starting")
            .ToListAsync();

        foreach (var p in staleProducts)
        {
            logger.LogInformation(
                "Preview startup: resetting {Product} preview state ({Status} port {Port}) → stopped",
                p.Name, p.PreviewStatus, p.PreviewPort);

            p.PreviewStatus = "stopped";
            p.PreviewPort   = null;
            p.PreviewError  = null;
            p.UpdatedAt     = DateTime.UtcNow;

            db.ActivityEvents.Add(new DomainActivity
            {
                ProductId = p.Id,
                EventType = ActivityType.PreviewStopped,
                Title     = "Preview detenido al reiniciar el backend",
                Details   = killed > 0
                    ? $"Proceso anterior terminado (cleanup). Podés reiniciar el preview."
                    : "Estado reseteado. Podés reiniciar el preview.",
            });
        }

        if (staleProducts.Count > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Preview startup: reset {Count} stale preview product(s)", staleProducts.Count);
        }
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

    private async Task StartInternalAsync(Guid productId, bool isRestart = false)
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

            await UpdateStateAsync(productId, "starting", null, null, DateTime.UtcNow, null,
                ActivityType.PreviewStarting,
                isRestart ? "Reiniciando preview automáticamente..." : "Iniciando preview del SaaS generado...");

            // npm install if node_modules missing
            if (!Directory.Exists(Path.Combine(frontendPath, "node_modules")))
            {
                logger.LogInformation("Running npm install for {ProductId} at {Path}", productId, frontendPath);
                await AddActivityAsync(productId, ActivityType.PreviewStarting, "Instalando dependencias (npm install)...",
                    "Primera vez — puede tardar 60-120s");
                await bus.StepAsync(productId, "Instalando dependencias (npm install)...");
                int exitCode = await RunCommandAsync("npm install", frontendPath, 180_000);
                if (exitCode != 0)
                { await SetErrorAsync(productId, "npm install falló. Verificá que Node.js esté instalado."); return; }
            }

            await bus.StepAsync(productId, "Iniciando servidor de preview...");

            int port = FindFreePort();
            if (port == -1)
            { await SetErrorAsync(productId, $"No hay puertos disponibles en el rango {PortRangeStart}-{PortRangeEnd}"); return; }

            logger.LogInformation("Starting preview for {ProductId} on port {Port} (restart={Restart})", productId, port, isRestart);

            var proc      = CreateProcess("npm run dev -- --port " + port, frontendPath);
            var outputBuf = new StringBuilder();
            var readyTcs  = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                outputBuf.AppendLine(e.Data);
                logger.LogDebug("[preview:{Id}] {Line}", productId, e.Data);
                if (e.Data.Contains("Ready",   StringComparison.OrdinalIgnoreCase) ||
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
            await RegisterPidAsync(productId, proc.Id, port, frontendPath);

            // Wait for ready: stdout signal OR port probe, whichever fires first (120s hard cap)
            using var cts          = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var portProbeTask      = ProbePortAsync(port, proc, cts.Token);
            var winner             = await Task.WhenAny(readyTcs.Task, portProbeTask, proc.WaitForExitAsync(cts.Token));

            if (proc.HasExited)
            {
                _running.TryRemove(productId, out _);
                await UnregisterPidAsync(productId);
                var tail = outputBuf.Length > 400 ? outputBuf.ToString()[^400..] : outputBuf.ToString();
                await SetErrorAsync(productId, $"Proceso terminó inesperadamente.\n{tail.Trim()}");
                return;
            }

            var url = $"http://localhost:{port}";
            await UpdateStateAsync(productId, "running", url, port, DateTime.UtcNow, null,
                ActivityType.PreviewRunning, $"Preview activo en {url}");

            // Start per-process watchdog — detects unexpected exits and auto-restarts once
            _ = Task.Run(() => WatchProcessAsync(productId, proc, isRestart));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Preview start failed for {ProductId}", productId);
            await SetErrorAsync(productId, ex.Message);
        }
    }

    // Port probe: parallel to stdout detection — tries TCP connection every 2s after 4s head start
    private static async Task ProbePortAsync(int port, Process proc, CancellationToken ct)
    {
        try { await Task.Delay(4000, ct); } catch (OperationCanceledException) { return; }

        while (!proc.HasExited && !ct.IsCancellationRequested)
        {
            try
            {
                using var client   = new TcpClient();
                using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                probeCts.CancelAfter(800);
                await client.ConnectAsync("127.0.0.1", port, probeCts.Token);
                return; // port is open — process is ready
            }
            catch { }

            try { await Task.Delay(2000, ct); } catch (OperationCanceledException) { return; }
        }
    }

    // Per-process watchdog: waits for process exit, updates DB state, auto-restarts once
    private async Task WatchProcessAsync(Guid productId, Process proc, bool wasRestart)
    {
        try { await proc.WaitForExitAsync(); }
        catch { return; }

        if (!_running.TryRemove(productId, out _))
            return; // StopAsync already cleaned this up — expected

        var code = proc.ExitCode;
        logger.LogWarning("Preview process exited for {ProductId} with code {Code}", productId, code);

        if (wasRestart)
        {
            // Already attempted one restart — don't loop
            await UnregisterPidAsync(productId);
            await UpdateStateAsync(productId, "error", null, null, null,
                $"Preview terminó (código {code}). Reiniciá manualmente.",
                ActivityType.PreviewError, "Preview caído — sin más reintentos automáticos");
            return;
        }

        // Auto-restart once
        await UpdateStateAsync(productId, "starting", null, null, null, null,
            ActivityType.PreviewStarting, $"Preview caído (código {code}) — reiniciando automáticamente...");

        await Task.Delay(2000);
        _ = Task.Run(() => StartInternalAsync(productId, isRestart: true));
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
        // Use IPGlobalProperties to detect occupied ports — avoids false-free reads caused by
        // Windows SO_REUSEADDR allowing loopback-bind even when 0.0.0.0:port is already taken.
        var occupied = IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Select(ep => ep.Port)
            .ToHashSet();

        for (int port = PortRangeStart; port <= PortRangeEnd; port++)
        {
            if (!occupied.Contains(port))
                return port;
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
        await bus.PingAsync(productId);
    }

    private async Task AddActivityAsync(Guid productId, ActivityType type, string title, string? details = null)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ActivityEvents.Add(new DomainActivity { ProductId = productId, EventType = type, Title = title, Details = details });
        await db.SaveChangesAsync();
    }

    // ── PID registry helpers ───────────────────────────────────────────────────

    private async Task RegisterPidAsync(Guid productId, int pid, int port, string frontendPath)
    {
        await _registryLock.WaitAsync();
        try
        {
            var entries = await ReadRegistryAsync();
            entries.RemoveAll(e => e.ProductId == productId);
            entries.Add(new PidEntry(productId, pid, port, frontendPath, DateTime.UtcNow));
            await File.WriteAllTextAsync(PidRegistryPath, JsonSerializer.Serialize(entries));
            logger.LogDebug("PID registry: registered PID {Pid} port {Port} for product {ProductId}", pid, port, productId);
        }
        catch (Exception ex) { logger.LogWarning(ex, "PID registry: failed to register PID {Pid}", pid); }
        finally { _registryLock.Release(); }
    }

    private async Task UnregisterPidAsync(Guid productId)
    {
        await _registryLock.WaitAsync();
        try
        {
            var entries = await ReadRegistryAsync();
            var removed = entries.RemoveAll(e => e.ProductId == productId);
            if (removed > 0)
            {
                if (entries.Count > 0)
                    await File.WriteAllTextAsync(PidRegistryPath, JsonSerializer.Serialize(entries));
                else if (File.Exists(PidRegistryPath))
                    File.Delete(PidRegistryPath);
                logger.LogDebug("PID registry: unregistered product {ProductId}", productId);
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "PID registry: failed to unregister product {ProductId}", productId); }
        finally { _registryLock.Release(); }
    }

    private static async Task<List<PidEntry>> ReadRegistryAsync()
    {
        if (!File.Exists(PidRegistryPath)) return [];
        try
        {
            var json = await File.ReadAllTextAsync(PidRegistryPath);
            return JsonSerializer.Deserialize<List<PidEntry>>(json) ?? [];
        }
        catch { return []; }
    }
}
