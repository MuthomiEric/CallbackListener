using CallbackListener.Application.Interfaces;
using CallbackListener.Configuration;
using CallbackListener.Domain;
using CallbackListener.Infrastructure.Data;
using CallbackListener.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CallbackListener.Application.Services;

public sealed class CallbackService : ICallbackService
{
    private readonly ICallbackStore _store;
    private readonly ICallbackCounter _counter;
    private readonly IAgentRegistry _registry;
    private readonly IHubContext<DashboardHub> _dashboard;
    private readonly IHubContext<AgentHub> _agentHub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CallbackService> _logger;
    private readonly int _localTimeoutSeconds;

    public CallbackService(
        ICallbackStore store,
        ICallbackCounter counter,
        IAgentRegistry registry,
        IHubContext<DashboardHub> dashboard,
        IHubContext<AgentHub> agentHub,
        IServiceScopeFactory scopeFactory,
        IOptions<AppOptions> options,
        ILogger<CallbackService> logger)
    {
        _store               = store;
        _counter             = counter;
        _registry            = registry;
        _dashboard           = dashboard;
        _agentHub            = agentHub;
        _scopeFactory        = scopeFactory;
        _logger              = logger;
        _localTimeoutSeconds = options.Value.LocalDeliveryTimeoutSeconds;
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
                relay    = new RelayTarget(listener.Scheme, listener.Port, listener.BasePath, listener.ClientId?.ToString());
                mode     = listener.Mode;
                userId   = listener.UserId;
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

        if (!string.IsNullOrEmpty(userId))
            _counter.Increment(userId);

        // Local-only mode: agent receives it, web feed does not.
        if (mode != DeliveryMode.Local)
        {
            _store.Add(entry);
            if (!string.IsNullOrEmpty(userId))
                await _dashboard.Clients.Group(userId).SendAsync("CallbackReceived", entry, ct);
        }

        // If agent was dispatched, auto-fail on dashboard if it doesn't report back in time
        if (entry.Status == CallbackStatus.Routed && !string.IsNullOrEmpty(userId))
            _ = AutoFailAsync(entry.Id, userId);

        _logger.LogInformation(
            "Callback {Id} [{Status}] — {Method} /collections/callback?slug={Slug} from {Ip}",
            entry.Id, entry.Status, entry.Method, entry.Slug, entry.SourceIp);

        return entry;
    }

    public async Task<CallbackEntry?> ResendAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var entry = _store.GetById(id, userId);
        if (entry is null) return null;

        var routed = await RouteAsync(entry with { Status = CallbackStatus.Received }, DeliveryMode.Both, ct);

        var updated = _store.UpdateStatus(id, userId, routed.Status, routed.StatusDetail);
        if (updated is not null)
            await _dashboard.Clients.Group(userId).SendAsync("CallbackUpdated", updated, ct);

        if (routed.Status == CallbackStatus.Routed)
            _ = AutoFailAsync(id, userId);

        return updated ?? routed;
    }

    private async Task AutoFailAsync(Guid id, string userId)
    {
        await Task.Delay(TimeSpan.FromSeconds(_localTimeoutSeconds));
        var updated = _store.UpdateStatus(id, userId, CallbackStatus.Dropped,
            "agent did not respond", onlyIfCurrent: CallbackStatus.Routed);
        if (updated is not null)
            await _dashboard.Clients.Group(userId).SendAsync("CallbackUpdated", updated);
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

        var clientId = entry.Relay?.ClientId;
        if (string.IsNullOrEmpty(clientId))
            return entry with { Status = CallbackStatus.Dropped, StatusDetail = "No client linked to this app" };

        var agent = _registry.GetByClientId(clientId);

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
                _logger.LogWarning(ex, "Failed to deliver callback {Id} to client {ClientId}", entry.Id, clientId);
                return entry with { Status = CallbackStatus.Dropped, StatusDetail = "Delivery failed" };
            }
        }

        var detail = agent is null ? "No agent connected for this client" : "Agent is offline";
        _logger.LogWarning("Callback {Id} dropped — {Detail} (slug: {Slug})", entry.Id, detail, entry.Slug);
        return entry with { Status = CallbackStatus.Dropped, StatusDetail = detail };
    }
}
