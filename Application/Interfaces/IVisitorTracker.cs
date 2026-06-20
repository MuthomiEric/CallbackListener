namespace CallbackListener.Application.Interfaces;

public interface IVisitorTracker
{
    void Track(string rawIp);
    Task<long> GetUniqueCountAsync();
}
