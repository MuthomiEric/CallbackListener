using CallbackListener.Domain;

namespace CallbackListener.Application.Interfaces;

public interface ICallbackStore
{
    void Add(CallbackEntry entry);
    IReadOnlyList<CallbackEntry> GetRecent(int count, string userId);
    CallbackEntry? GetById(Guid id, string userId);
    void Clear(string userId);
    CallbackEntry? UpdateStatus(Guid id, string userId, CallbackStatus status, string? detail, CallbackStatus? onlyIfCurrent = null);
}
