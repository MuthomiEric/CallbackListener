using System.Security.Claims;
using CallbackListener.Application.Interfaces;

namespace CallbackListener.Web.Endpoints;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/callbacks", (HttpContext ctx, ICallbackStore store, int count = 50) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            return Results.Ok(store.GetRecent(Math.Clamp(count, 1, 200), userId));
        });

        api.MapDelete("/callbacks", (HttpContext ctx, ICallbackStore store) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            store.Clear(userId);
            return Results.NoContent();
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
