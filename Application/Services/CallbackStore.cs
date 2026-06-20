using CallbackListener.Application.Interfaces;
using CallbackListener.Domain;

namespace CallbackListener.Application.Services;

public sealed class CallbackStore : ICallbackStore
{
    private const int MaxPerUser = 5;

    private readonly Dictionary<string, LinkedList<CallbackEntry>> _byUser = new();
    private readonly object _lock = new();

    public CallbackStore() { }

    public void Add(CallbackEntry entry)
    {
        lock (_lock)
        {
            if (!_byUser.TryGetValue(entry.UserId, out var list))
                _byUser[entry.UserId] = list = new LinkedList<CallbackEntry>();

            list.AddFirst(entry);
            if (list.Count > MaxPerUser)
                list.RemoveLast();
        }
    }

    public CallbackEntry? GetById(Guid id, string userId)
    {
        lock (_lock)
        {
            if (!_byUser.TryGetValue(userId, out var list)) return null;
            var node = list.First;
            while (node is not null)
            {
                if (node.Value.Id == id) return node.Value;
                node = node.Next;
            }
            return null;
        }
    }

    public IReadOnlyList<CallbackEntry> GetRecent(int count, string userId)
    {
        lock (_lock)
        {
            if (!_byUser.TryGetValue(userId, out var list))
                return [];

            return list.Take(Math.Min(count, MaxPerUser)).ToList();
        }
    }

    public void Clear(string userId)
    {
        lock (_lock)
            _byUser.Remove(userId);
    }

    public CallbackEntry? UpdateStatus(Guid id, string userId, CallbackStatus status, string? detail, CallbackStatus? onlyIfCurrent = null)
    {
        lock (_lock)
        {
            if (!_byUser.TryGetValue(userId, out var list)) return null;
            var node = list.First;
            while (node is not null)
            {
                if (node.Value.Id == id)
                {
                    if (onlyIfCurrent.HasValue && node.Value.Status != onlyIfCurrent.Value) return null;
                    node.Value = node.Value with { Status = status, StatusDetail = detail };
                    return node.Value;
                }
                node = node.Next;
            }
            return null;
        }
    }
}
