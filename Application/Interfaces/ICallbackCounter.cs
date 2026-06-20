namespace CallbackListener.Application.Interfaces;

public interface ICallbackCounter
{
    void Increment(string userId);
    IReadOnlyDictionary<string, long> DrainDeltas();
}
