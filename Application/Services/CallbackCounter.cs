using System.Collections.Concurrent;
using CallbackListener.Application.Interfaces;

namespace CallbackListener.Application.Services;

public sealed class CallbackCounter : ICallbackCounter
{
    private readonly ConcurrentDictionary<string, long> _deltas = new();

    public void Increment(string userId)
    {
        if (!string.IsNullOrEmpty(userId))
            _deltas.AddOrUpdate(userId, 1L, (_, v) => v + 1);
    }

    public IReadOnlyDictionary<string, long> DrainDeltas()
    {
        var snapshot = new Dictionary<string, long>(_deltas.Count);
        foreach (var key in _deltas.Keys.ToList())
            if (_deltas.TryRemove(key, out var val))
                snapshot[key] = val;
        return snapshot;
    }
}
