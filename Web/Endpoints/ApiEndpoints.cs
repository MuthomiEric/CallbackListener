using System.Security.Claims;
using CallbackListener.Application.Interfaces;
using CallbackListener.Infrastructure.Data;
using CallbackListener.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace CallbackListener.Web.Endpoints;

public static class ApiEndpoints
{
    // Binaries and install scripts served explicitly so the /{**subpath} webhook
    // catch-all never swallows them (it returns 400 when no ?slug= is present).
    private static readonly HashSet<string> AllowedDownloads = new(StringComparer.OrdinalIgnoreCase)
    {
        "CallbackAgent-linux-x64",
        "CallbackAgent-linux-arm64",
        "CallbackAgent-win-x64.exe",
    };

    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapGet("/install.sh", (IWebHostEnvironment env) =>
        {
            var path = Path.Combine(env.WebRootPath, "install.sh");
            return File.Exists(path) ? Results.File(path, "text/plain; charset=utf-8") : Results.NotFound();
        });

        app.MapGet("/install.ps1", (IWebHostEnvironment env) =>
        {
            var path = Path.Combine(env.WebRootPath, "install.ps1");
            return File.Exists(path) ? Results.File(path, "text/plain; charset=utf-8") : Results.NotFound();
        });

        app.MapGet("/downloads/{file}", (string file, IWebHostEnvironment env) =>
        {
            if (!AllowedDownloads.Contains(file)) return Results.NotFound();
            var path = Path.Combine(env.WebRootPath, "downloads", file);
            return File.Exists(path)
                ? Results.File(path, "application/octet-stream", file)
                : Results.NotFound();
        });

        // Agent key validation — no cookie auth, key in header
        app.MapGet("/api/agent/check", async (HttpContext ctx, AppDbContext db) =>
        {
            var apiKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault()
                      ?? ctx.Request.Query["apiKey"].FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey)) return Results.Unauthorized();
            var hash = KeyHasher.Hash(apiKey);
            return await db.Clients.AnyAsync(c => c.KeyHash == hash) ? Results.Ok() : Results.Unauthorized();
        });

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
