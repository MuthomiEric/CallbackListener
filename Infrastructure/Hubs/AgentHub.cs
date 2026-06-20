using CallbackListener.Application.Interfaces;
using CallbackListener.Domain;
using CallbackListener.Infrastructure.Data;
using CallbackListener.Infrastructure.Security;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CallbackListener.Infrastructure.Hubs;

/// <summary>
/// Agent connections via SignalR. Agents authenticate with an API key:
///   /hubs/agents?apiKey={key}  or  X-Api-Key header
/// One agent handles all slugs owned by the key's user. clientId = userId.
/// </summary>
public sealed class AgentHub : Hub
{
    private readonly IAgentRegistry _registry;
    private readonly IHubContext<DashboardHub> _dashboard;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(
        IAgentRegistry registry,
        IHubContext<DashboardHub> dashboard,
        IServiceScopeFactory scopeFactory,
        ILogger<AgentHub> logger)
    {
        _registry     = registry;
        _dashboard    = dashboard;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var apiKey = httpContext?.Request.Headers["X-Api-Key"].FirstOrDefault()
                  ?? httpContext?.Request.Query["apiKey"].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Agent connection rejected — no API key from {Ip}",
                httpContext?.Connection.RemoteIpAddress);
            Context.Abort();
            return;
        }

        string clientId;
        string userId;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hash = KeyHasher.Hash(apiKey);
            var client = await db.Clients.FirstOrDefaultAsync(c => c.KeyHash == hash);

            if (client is null)
            {
                _logger.LogWarning("Agent connection rejected — invalid API key from {Ip}",
                    httpContext?.Connection.RemoteIpAddress);
                Context.Abort();
                return;
            }

            client.LastUsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            clientId = client.Id.ToString();
            userId   = client.UserId;
        }

        if (_registry.IsOnline(clientId))
        {
            _logger.LogWarning("Agent connection rejected — key already active from another instance ({ClientId}) from {Ip}",
                clientId, httpContext?.Connection.RemoteIpAddress);
            Context.Abort();
            return;
        }

        Context.Items["clientId"] = clientId;
        Context.Items["userId"]   = userId;

        var agent = new AgentInfo
        {
            ClientId     = clientId,
            ConnectionId = Context.ConnectionId,
            ConnectedAt  = DateTimeOffset.UtcNow,
            LastSeenAt   = DateTimeOffset.UtcNow,
            IsOnline     = true
        };

        _registry.Register(agent);

        _logger.LogInformation("Agent connected for user {UserId} ({ConnectionId}) from {Ip}",
            userId, Context.ConnectionId, httpContext?.Connection.RemoteIpAddress);

        await _dashboard.Clients.Group(userId).SendAsync("AgentStatusChanged", agent);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _registry.Unregister(Context.ConnectionId);

        var clientId = Context.Items["clientId"] as string;
        var userId   = Context.Items["userId"]   as string;
        if (clientId is not null)
        {
            _logger.LogInformation("Agent {ClientId} disconnected ({ConnectionId})",
                clientId, Context.ConnectionId);

            var agent = _registry.GetByClientId(clientId);
            if (agent is not null && !string.IsNullOrEmpty(userId))
                await _dashboard.Clients.Group(userId).SendAsync("AgentStatusChanged", agent);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task PingAsync()
    {
        var clientId = Context.Items["clientId"] as string;
        if (clientId is not null)
        {
            var agent = _registry.GetByClientId(clientId);
            if (agent is not null)
                agent.LastSeenAt = DateTimeOffset.UtcNow;
        }
        return Task.CompletedTask;
    }
}
