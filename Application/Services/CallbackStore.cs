using CallbackListener.Application.Interfaces;
using CallbackListener.Configuration;
using CallbackListener.Domain;
using Microsoft.Extensions.Options;

namespace CallbackListener.Application.Services;

public sealed class CallbackStore : ICallbackStore
{
    private readonly LinkedList<CallbackEntry> _entries = new();
    private readonly object _lock = new();
    private readonly int _maxCount;

    public CallbackStore(IOptions<AppOptions> options)
    {
        _maxCount = options.Value.MaxHistoryCount;
    }

    public void Add(CallbackEntry entry)
    {
        lock (_lock)
        {
            _entries.AddFirst(entry);
            while (_entries.Count > _maxCount)
                _entries.RemoveLast();
        }
    }

    public IReadOnlyList<CallbackEntry> GetRecent(int count, string userId)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.UserId == userId)
                .Take(Math.Min(count, _maxCount))
                .ToList();
        }
    }
}
