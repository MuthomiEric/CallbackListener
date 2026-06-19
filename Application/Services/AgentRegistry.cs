using System.Collections.Concurrent;
using CallbackListener.Application.Interfaces;
using CallbackListener.Domain;

namespace CallbackListener.Application.Services;

public sealed class AgentRegistry : IAgentRegistry
{
    // clientId → AgentInfo
    private readonly ConcurrentDictionary<string, AgentInfo> _byClientId = new();
    // connectionId → clientId
    private readonly ConcurrentDictionary<string, string> _connectionToClient = new();

    public void Register(AgentInfo agent)
    {
        // Clean up any stale connection mapping for this clientId (reconnect scenario)
        if (_byClientId.TryGetValue(agent.ClientId, out var existing))
            _connectionToClient.TryRemove(existing.ConnectionId, out _);

        _byClientId[agent.ClientId] = agent;
        _connectionToClient[agent.ConnectionId] = agent.ClientId;
    }

    public void Unregister(string connectionId)
    {
        if (_connectionToClient.TryRemove(connectionId, out var clientId))
        {
            if (_byClientId.TryGetValue(clientId, out var agent))
                _byClientId[clientId] = agent with { IsOnline = false };
        }
    }

    public AgentInfo? GetByClientId(string clientId) =>
        _byClientId.TryGetValue(clientId, out var agent) ? agent : null;

    public IReadOnlyList<AgentInfo> GetAll() =>
        _byClientId.Values.OrderByDescending(a => a.ConnectedAt).ToList();

    public bool IsOnline(string clientId) =>
        _byClientId.TryGetValue(clientId, out var agent) && agent.IsOnline;
}
