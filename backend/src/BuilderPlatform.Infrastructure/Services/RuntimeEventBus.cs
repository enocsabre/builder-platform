using System.Collections.Concurrent;

namespace BuilderPlatform.Infrastructure.Services;

public class RuntimeEventBus
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Func<string, string, Task>>> _subs = new();

    public IDisposable Subscribe(Guid productId, Func<string, string, Task> handler)
    {
        var subId = Guid.NewGuid();
        _subs.GetOrAdd(productId, _ => new())[subId] = handler;
        return new Lease(() =>
        {
            if (_subs.TryGetValue(productId, out var bag)) bag.TryRemove(subId, out _);
        });
    }

    public Task PingAsync(Guid productId)                  => BroadcastAsync(productId, "ping",      "");
    public Task StepAsync(Guid productId, string title)    => BroadcastAsync(productId, "step",      title);

    private async Task BroadcastAsync(Guid productId, string eventType, string data)
    {
        if (!_subs.TryGetValue(productId, out var handlers)) return;
        foreach (var (_, h) in handlers.ToArray())
            try { await h(eventType, data); }
            catch { /* subscriber gone — no-op */ }
    }

    private sealed class Lease(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
