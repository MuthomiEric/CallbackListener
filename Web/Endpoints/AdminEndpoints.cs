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

        group.MapGet("/users", async (
            HttpContext ctx,
            AppDbContext db,
            UserManager<AppUser> userMgr,
            IAgentRegistry registry,
            ICallbackStore store,
            IOptions<AppOptions> opts) =>
        {
            var adminEmail = opts.Value.AdminEmail;
            if (string.IsNullOrEmpty(adminEmail)) return Results.Forbid();

            var caller = await userMgr.GetUserAsync(ctx.User);
            if (caller is null || caller.Email != adminEmail) return Results.Forbid();

            var users = await db.Users
                .Include(u => u.Listeners)
                .Include(u => u.Clients)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var rows = users.Select(u =>
            {
                var isOnline = u.Clients.Any(c => registry.IsOnline(c.Id.ToString()));
                var lastCb   = store.GetRecent(1, u.Id).FirstOrDefault();
                return new
                {
                    id            = u.Id,
                    email         = u.Email,
                    displayName   = u.DisplayName,
                    createdAt     = u.CreatedAt,
                    appCount      = u.Listeners.Count,
                    clientCount   = u.Clients.Count,
                    isOnline,
                    lastCallback  = lastCb?.Timestamp,
                };
            });

            return Results.Ok(rows);
        });
    }
}
