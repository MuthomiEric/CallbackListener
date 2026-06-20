using CallbackListener.Application.Interfaces;
using CallbackListener.Configuration;
using CallbackListener.Domain;
using CallbackListener.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CallbackListener.Web.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization();

        group.MapGet("/visitors", async (IVisitorTracker tracker) =>
            Results.Ok(new { uniqueVisitors = await tracker.GetUniqueCountAsync() }));

        group.MapGet("/users", async (
            HttpContext ctx,
            AppDbContext db,
            UserManager<AppUser> userMgr,
            IAgentRegistry registry,
            ICallbackStore store,
            IOptions<AppOptions> opts,
            int page = 1,
            int size = 5) =>
        {
            var adminEmail = opts.Value.AdminEmail;
            if (string.IsNullOrEmpty(adminEmail)) return Results.Forbid();

            var caller = await userMgr.GetUserAsync(ctx.User);
            if (caller is null || caller.Email != adminEmail) return Results.Forbid();

            size = Math.Clamp(size, 5, 20);
            page = Math.Max(1, page);

            var total          = await db.Users.CountAsync();
            var totalApps      = await db.Listeners.CountAsync();
            var totalCallbacks = await db.Users.SumAsync(u => (long?)u.TotalCallbacksReceived) ?? 0;

            var allClients = await db.Clients
                .Select(c => new { c.Id, c.UserId })
                .ToListAsync();

            var onlineCountByUser = allClients
                .Where(c => registry.IsOnline(c.Id.ToString()))
                .GroupBy(c => c.UserId)
                .ToDictionary(g => g.Key, g => g.Count());

            var liveUserIds = onlineCountByUser.Keys.ToHashSet();

            var users = await db.Users
                .Include(u => u.Listeners)
                .Include(u => u.Clients)
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var rows = users.Select(u =>
            {
                onlineCountByUser.TryGetValue(u.Id, out var onlineAgents);
                var lastCb = store.GetRecent(1, u.Id).FirstOrDefault();
                return new
                {
                    id                     = u.Id,
                    email                  = u.Email,
                    displayName            = u.DisplayName,
                    createdAt              = u.CreatedAt,
                    appCount               = u.Listeners.Count,
                    clientCount            = u.Clients.Count,
                    totalCallbacksReceived = u.TotalCallbacksReceived,
                    onlineAgents,
                    lastCallback           = lastCb?.Timestamp,
                };
            });

            return Results.Ok(new { total, page, size, totalApps, totalCallbacks, liveAgents = liveUserIds.Count, rows });
        });
    }
}
