using System.Security.Claims;
using CallbackListener.Application.Interfaces;
using CallbackListener.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CallbackListener.Infrastructure.Hubs;

/// <summary>
/// Browser dashboard connections.
/// Each authenticated user is placed in a SignalR group keyed by their user ID
/// so CallbackReceived and AgentStatusChanged events are scoped per account.
/// </summary>
public sealed class DashboardHub : Hub
{
    private readonly ICallbackStore _store;
    private readonly IAgentRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(
        ICallbackStore store,
        IAgentRegistry registry,
        IServiceScopeFactory scopeFactory,
        ILogger<DashboardHub> logger)
    {
        _store        = store;
        _registry     = registry;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetHttpContext()?.User
                           .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Dashboard connection rejected — unauthenticated ({ConnectionId})", Context.ConnectionId);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        _logger.LogDebug("Dashboard connected: {ConnectionId} user={UserId}", Context.ConnectionId, userId);

        await Clients.Caller.SendAsync("History", _store.GetRecent(50, userId));

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userClientIds = await db.Clients
            .Where(c => c.UserId == userId)
            .Select(c => c.Id.ToString())
            .ToListAsync();

        var userAgents = _registry.GetAll()
            .Where(a => userClientIds.Contains(a.ClientId))
            .ToList();

        await Clients.Caller.SendAsync("AgentList", userAgents);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.GetHttpContext()?.User
                           .FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);

        _logger.LogDebug("Dashboard disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
