using System.Security.Claims;
using System.Security.Cryptography;
using CallbackListener.Application.Interfaces;
using CallbackListener.Domain;
using CallbackListener.Infrastructure.Data;
using CallbackListener.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CallbackListener.Web.Endpoints;

public static class ClientEndpoints
{
    public static void MapClientEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/clients").RequireAuthorization();

        group.MapGet("/", async (HttpContext ctx, AppDbContext db, IAgentRegistry registry, IMemoryCache cache) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var cacheKey = $"clients:{userId}";
            if (!cache.TryGetValue(cacheKey, out List<Client>? clients))
            {
                clients = await db.Clients
                    .Where(c => c.UserId == userId)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
                cache.Set(cacheKey, clients, TimeSpan.FromSeconds(10));
            }

            return Results.Ok(clients!.Select(c => new
            {
                id         = c.Id,
                label      = c.Label,
                masked     = "cr_live_••••" + c.KeySuffix,
                isOnline   = registry.IsOnline(c.Id.ToString()),
                createdAt  = c.CreatedAt,
                lastUsedAt = c.LastUsedAt,
            }));
        });

        group.MapPost("/", async (CreateClientRequest req, HttpContext ctx, AppDbContext db, IMemoryCache cache) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Label))
                return Results.BadRequest(new { error = "Label is required" });

            var rawKey = "cr_live_" + GenerateHex(32);
            var client = new Client
            {
                UserId    = userId,
                Label     = req.Label.Trim(),
                KeyHash   = KeyHasher.Hash(rawKey),
                KeySuffix = rawKey[^4..],
            };

            db.Clients.Add(client);
            await db.SaveChangesAsync();
            cache.Remove($"clients:{userId}");

            return Results.Ok(new
            {
                id        = client.Id,
                label     = client.Label,
                key       = rawKey,
                masked    = "cr_live_••••" + client.KeySuffix,
                createdAt = client.CreatedAt,
            });
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db, IMemoryCache cache) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (client is null) return Results.NotFound();

            db.Clients.Remove(client);
            await db.SaveChangesAsync();
            cache.Remove($"clients:{userId}");
            return Results.NoContent();
        });
    }

    private static string GenerateHex(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length / 2);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

record CreateClientRequest(string Label);
