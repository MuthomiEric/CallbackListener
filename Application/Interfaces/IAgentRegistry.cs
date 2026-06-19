using CallbackListener.Domain;

namespace CallbackListener.Application.Interfaces;

public interface IAgentRegistry
{
    void Register(AgentInfo agent);
    void Unregister(string connectionId);
    AgentInfo? GetByClientId(string clientId);
    IReadOnlyList<AgentInfo> GetAll();
    bool IsOnline(string clientId);
}
