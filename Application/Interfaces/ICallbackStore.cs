using CallbackListener.Domain;

namespace CallbackListener.Application.Interfaces;

public interface ICallbackStore
{
    void Add(CallbackEntry entry);
    IReadOnlyList<CallbackEntry> GetRecent(int count, string userId);
    void Clear(string userId);
}
