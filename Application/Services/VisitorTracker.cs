using System.Collections.Concurrent;
using CallbackListener.Application.Interfaces;
using CallbackListener.Domain;
using CallbackListener.Infrastructure.Data;
using CallbackListener.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace CallbackListener.Application.Services;

public sealed class VisitorTracker : IVisitorTracker
{
    private readonly ConcurrentDictionary<string, byte> _seen = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisitorTracker> _logger;

    public VisitorTracker(IServiceScopeFactory scopeFactory, ILogger<VisitorTracker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public void Track(string rawIp)
    {
        if (string.IsNullOrEmpty(rawIp) || rawIp == "unknown") return;
        var hash = KeyHasher.Hash(rawIp);
        if (!_seen.TryAdd(hash, 0)) return;
        _ = PersistIpAsync(hash);
    }

    public async Task<long> GetUniqueCountAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.VisitorIps.LongCountAsync();
    }

    private async Task PersistIpAsync(string hash)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "VisitorIps" ("IpHash", "FirstSeenAt")
                VALUES ({0}, {1})
                ON CONFLICT DO NOTHING
                """,
                hash, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist visitor IP");
            _seen.TryRemove(hash, out _);
        }
    }
}
