using System.Security.Claims;
using CallbackListener.Application.Interfaces;
using CallbackListener.Domain;
using CallbackListener.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CallbackListener.Web.Endpoints;

public static class ListenerEndpoints
{
    public static void MapListenerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/apps").RequireAuthorization();

        group.MapGet("/", async (HttpContext ctx, AppDbContext db, IAgentRegistry registry, IMemoryCache cache) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var cacheKey = $"apps:{userId}";
            if (!cache.TryGetValue(cacheKey, out List<Listener>? rows))
            {
                rows = await db.Listeners
                    .Where(l => l.UserId == userId)
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync();
                cache.Set(cacheKey, rows, TimeSpan.FromSeconds(10));
            }

            return Results.Ok(rows!.Select(l => new
            {
                id        = l.Id,
                slug      = l.Slug,
                label     = l.Label,
                scheme    = l.Scheme,
                port      = l.Port,
                basePath  = l.BasePath,
                mode      = (int)l.Mode,
                clientId  = l.ClientId,
                isActive  = l.ClientId.HasValue && registry.IsOnline(l.ClientId.Value.ToString()),
                createdAt = l.CreatedAt,
            }));
        });

        group.MapPost("/", async (CreateListenerRequest req, HttpContext ctx, AppDbContext db, IMemoryCache cache) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var slug = req.Slug.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(slug))
                return Results.BadRequest(new { error = "Slug is required" });

            if (await db.Listeners.AnyAsync(l => l.Slug == slug))
                return Results.Conflict(new { error = "Slug already taken" });

            if (req.ClientId.HasValue)
            {
                var ownsClient = await db.Clients.AnyAsync(c => c.Id == req.ClientId && c.UserId == userId);
                if (!ownsClient) return Results.BadRequest(new { error = "Invalid client" });
            }

            var listener = new Listener
            {
                UserId   = userId,
                Slug     = slug,
                Label    = req.Label.Trim(),
                Scheme   = req.Scheme == "https" ? "https" : "http",
                Port     = req.Port,
                BasePath = string.IsNullOrWhiteSpace(req.BasePath) ? "/" : req.BasePath.Trim(),
                Mode     = Enum.IsDefined(typeof(DeliveryMode), req.Mode) ? (DeliveryMode)req.Mode : DeliveryMode.WebOnly,
                ClientId = req.ClientId,
            };

            db.Listeners.Add(listener);
            await db.SaveChangesAsync();
            cache.Remove($"apps:{userId}");

            return Results.Created($"/api/apps/{listener.Id}", new
            {
                id       = listener.Id,
                slug     = listener.Slug,
                label    = listener.Label,
                scheme   = listener.Scheme,
                port     = listener.Port,
                basePath = listener.BasePath,
                mode     = (int)listener.Mode,
                clientId = listener.ClientId,
                isActive = false,
            });
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateListenerRequest req, HttpContext ctx, AppDbContext db, IMemoryCache cache) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var listener = await db.Listeners.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);
            if (listener is null) return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(req.Label))
                listener.Label = req.Label.Trim();
            if (!string.IsNullOrWhiteSpace(req.Scheme))
                listener.Scheme = req.Scheme == "https" ? "https" : "http";
            if (req.Port is > 0)
                listener.Port = req.Port.Value;
            if (req.BasePath is not null)
                listener.BasePath = string.IsNullOrWhiteSpace(req.BasePath) ? "/" : req.BasePath.Trim();

            if (req.ClientId is not null)
            {
                if (req.ClientId == Guid.Empty)
                {
                    listener.ClientId = null;
                }
                else
                {
                    var ownsClient = await db.Clients.AnyAsync(c => c.Id == req.ClientId && c.UserId == userId);
                    if (!ownsClient) return Results.BadRequest(new { error = "Invalid client" });
                    listener.ClientId = req.ClientId;
                }
            }

            await db.SaveChangesAsync();
            cache.Remove($"apps:{userId}");
            return Results.Ok(new
            {
                id       = listener.Id,
                slug     = listener.Slug,
                label    = listener.Label,
                scheme   = listener.Scheme,
                port     = listener.Port,
                basePath = listener.BasePath,
                mode     = (int)listener.Mode,
                clientId = listener.ClientId,
            });
        });

        group.MapPatch("/{id:guid}/mode", async (Guid id, ModeRequest req, HttpContext ctx, AppDbContext db, IMemoryCache cache) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var listener = await db.Listeners.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);
            if (listener is null) return Results.NotFound();

            if (!Enum.IsDefined(typeof(DeliveryMode), req.Mode))
                return Results.BadRequest(new { error = "Invalid mode" });

            listener.Mode = (DeliveryMode)req.Mode;
            await db.SaveChangesAsync();
            cache.Remove($"apps:{userId}");
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db, IMemoryCache cache) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var listener = await db.Listeners.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);
            if (listener is null) return Results.NotFound();

            db.Listeners.Remove(listener);
            await db.SaveChangesAsync();
            cache.Remove($"apps:{userId}");
            return Results.NoContent();
        });
    }
}

record CreateListenerRequest(string Slug, string Label, string Scheme, int Port, string? BasePath, int Mode = 0, Guid? ClientId = null);
record UpdateListenerRequest(string? Label, string? Scheme, int? Port, string? BasePath, Guid? ClientId = null);
record ModeRequest(int Mode);
