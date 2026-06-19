using CallbackListener.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using CallbackListener.Domain;

namespace CallbackListener.Web.Endpoints;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/callbacks", async (HttpContext ctx, ICallbackStore store, UserManager<AppUser> userMgr, int count = 50) =>
        {
            var user = await userMgr.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();
            return Results.Ok(store.GetRecent(Math.Clamp(count, 1, 200), user.Id));
        });

        api.MapGet("/agents", (IAgentRegistry registry) =>
            Results.Ok(registry.GetAll()));

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            time   = DateTimeOffset.UtcNow
        }));
    }
}
