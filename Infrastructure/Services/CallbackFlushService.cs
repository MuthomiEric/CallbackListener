using CallbackListener.Application.Interfaces;
using CallbackListener.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CallbackListener.Infrastructure.Services;

public sealed class CallbackFlushService : BackgroundService
{
    private readonly ICallbackCounter _counter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CallbackFlushService> _logger;

    public CallbackFlushService(
        ICallbackCounter counter,
        IServiceScopeFactory scopeFactory,
        ILogger<CallbackFlushService> logger)
    {
        _counter      = counter;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            await FlushAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await FlushAsync(); // persist any counts accumulated since the last tick
    }

    private async Task FlushAsync()
    {
        var deltas = _counter.DrainDeltas();
        if (deltas.Count == 0) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var (userId, delta) in deltas)
            {
                await db.Users
                    .Where(u => u.Id == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        u => u.TotalCallbacksReceived,
                        u => u.TotalCallbacksReceived + delta));
            }
            _logger.LogDebug("Flushed callback counts for {Count} users", deltas.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush callback counts — deltas lost for this interval");
        }
    }
}
