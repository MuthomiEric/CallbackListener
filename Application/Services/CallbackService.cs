using CallbackListener.Application.Interfaces;
using CallbackListener.Domain;
using CallbackListener.Infrastructure.Data;
using CallbackListener.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CallbackListener.Application.Services;

public sealed class CallbackService : ICallbackService
{
    private readonly ICallbackStore _store;
    private readonly IAgentRegistry _registry;
    private readonly IHubContext<DashboardHub> _dashboard;
    private readonly IHubContext<AgentHub> _agentHub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CallbackService> _logger;

    public CallbackService(
        ICallbackStore store,
        IAgentRegistry registry,
        IHubContext<DashboardHub> dashboard,
        IHubContext<AgentHub> agentHub,
        IServiceScopeFactory scopeFactory,
        ILogger<CallbackService> logger)
    {
        _store       = store;
        _registry    = registry;
        _dashboard   = dashboard;
        _agentHub    = agentHub;
        _scopeFactory = scopeFactory;
        _logger      = logger;
    }

    public async Task<CallbackEntry> ProcessAsync(CallbackContext ctx, CancellationToken ct = default)
    {
        RelayTarget? relay = null;
        var mode   = DeliveryMode.Both;
        var userId = string.Empty;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var listener = await db.Listeners.FirstOrDefaultAsync(l => l.Slug == ctx.Slug, ct);
            if (listener is not null)
            {
                relay  = new RelayTarget(listener.Scheme, listener.Port, listener.BasePath);
                mode   = listener.Mode;
                userId = listener.UserId;
            }
        }

        var entry = new CallbackEntry
        {
            UserId      = userId,
            Slug        = ctx.Slug,
            SubPath     = ctx.SubPath,
            Method      = ctx.Method,
            SourceIp    = ctx.SourceIp,
            ContentType = ctx.ContentType,
            Headers     = ctx.Headers,
            Query       = ctx.Query,
            RawBody     = ctx.RawBody,
            IsJsonBody  = ctx.IsJsonBody,
            Timestamp   = DateTimeOffset.UtcNow,
            Relay       = relay
        };

        entry = await RouteAsync(entry, mode, ct);

        _store.Add(entry);

        // Push only to the dashboard connections belonging to the app owner.
        if (!string.IsNullOrEmpty(userId))
            await _dashboard.Clients.Group(userId).SendAsync("CallbackReceived", entry, ct);

        _logger.LogInformation(
            "Callback {Id} [{Status}] — {Method} /collections/callback?slug={Slug} from {Ip}",
            entry.Id, entry.Status, entry.Method, entry.Slug, entry.SourceIp);

        return entry;
    }

    private async Task<CallbackEntry> RouteAsync(CallbackEntry entry, DeliveryMode mode, CancellationToken ct)
    {
        if (entry.Relay is null)
        {
            return entry with
            {
                Status       = CallbackStatus.Received,
                StatusDetail = "No app registered for this slug"
            };
        }

        if (mode == DeliveryMode.WebOnly)
        {
            return entry with
            {
                Status       = CallbackStatus.Received,
                StatusDetail = "Web-only mode"
            };
        }

        var agent = _registry.GetByClientId(entry.UserId);

        if (agent is { IsOnline: true })
        {
            try
            {
                await _agentHub.Clients.Client(agent.ConnectionId)
                    .SendAsync("CallbackReceived", entry, ct);

                return entry with { Status = CallbackStatus.Routed };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver callback {Id} to agent for user {UserId}", entry.Id, entry.UserId);
                return entry with { Status = CallbackStatus.Dropped, StatusDetail = "Delivery failed" };
            }
        }

        var detail = agent is null ? "No agent connected" : "Agent is offline";
        _logger.LogWarning("Callback {Id} dropped — {Detail} (slug: {Slug})", entry.Id, detail, entry.Slug);
        return entry with { Status = CallbackStatus.Dropped, StatusDetail = detail };
    }
}
