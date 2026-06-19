using CallbackListener.Application.Interfaces;
using CallbackListener.Domain;
using CallbackListener.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CallbackListener.Web.Endpoints;

public static class ListenerEndpoints
{
    public static void MapListenerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/apps").RequireAuthorization();

        group.MapGet("/", async (HttpContext ctx, AppDbContext db, UserManager<AppUser> userMgr, IAgentRegistry registry) =>
        {
            var user = await userMgr.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();

            var rows = await db.Listeners
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            return Results.Ok(rows.Select(l => new
            {
                id        = l.Id,
                slug      = l.Slug,
                label     = l.Label,
                scheme    = l.Scheme,
                port      = l.Port,
                mode      = (int)l.Mode,
                isActive  = registry.GetByClientId(l.Slug)?.IsOnline ?? false,
                createdAt = l.CreatedAt,
            }));
        });

        group.MapPost("/", async (CreateListenerRequest req, HttpContext ctx, AppDbContext db, UserManager<AppUser> userMgr) =>
        {
            var user = await userMgr.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();

            var slug = req.Slug.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(slug))
                return Results.BadRequest(new { error = "Slug is required" });

            if (await db.Listeners.AnyAsync(l => l.Slug == slug))
                return Results.Conflict(new { error = "Slug already taken" });

            var listener = new Listener
            {
                UserId   = user.Id,
                Slug     = slug,
                Label    = req.Label.Trim(),
                Scheme   = req.Scheme == "https" ? "https" : "http",
                Port     = req.Port,
                BasePath = string.IsNullOrWhiteSpace(req.BasePath) ? "/" : req.BasePath.Trim(),
                Mode     = Enum.IsDefined(typeof(DeliveryMode), req.Mode) ? (DeliveryMode)req.Mode : DeliveryMode.Both,
            };

            db.Listeners.Add(listener);
            await db.SaveChangesAsync();

            return Results.Created($"/api/apps/{listener.Id}", new
            {
                id       = listener.Id,
                slug     = listener.Slug,
                label    = listener.Label,
                scheme   = listener.Scheme,
                port     = listener.Port,
                mode     = (int)listener.Mode,
                isActive = false,
            });
        });

        group.MapPatch("/{id:guid}/mode", async (Guid id, ModeRequest req, HttpContext ctx, AppDbContext db, UserManager<AppUser> userMgr) =>
        {
            var user = await userMgr.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();

            var listener = await db.Listeners.FirstOrDefaultAsync(l => l.Id == id && l.UserId == user.Id);
            if (listener is null) return Results.NotFound();

            if (!Enum.IsDefined(typeof(DeliveryMode), req.Mode))
                return Results.BadRequest(new { error = "Invalid mode" });

            listener.Mode = (DeliveryMode)req.Mode;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db, UserManager<AppUser> userMgr) =>
        {
            var user = await userMgr.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();

            var listener = await db.Listeners.FirstOrDefaultAsync(l => l.Id == id && l.UserId == user.Id);
            if (listener is null) return Results.NotFound();

            db.Listeners.Remove(listener);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

record CreateListenerRequest(string Slug, string Label, string Scheme, int Port, string? BasePath, int Mode = 2);
record ModeRequest(int Mode);
