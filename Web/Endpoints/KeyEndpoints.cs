using System.Security.Cryptography;
using CallbackListener.Domain;
using CallbackListener.Infrastructure.Data;
using CallbackListener.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CallbackListener.Web.Endpoints;

public static class KeyEndpoints
{
    public static void MapKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/keys").RequireAuthorization();

        group.MapGet("/", async (HttpContext ctx, AppDbContext db, UserManager<AppUser> userMgr) =>
        {
            var user = await userMgr.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();

            var keys = await db.ApiKeys
                .Where(k => k.UserId == user.Id)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new
                {
                    id         = k.Id,
                    label      = k.Label,
                    masked     = "cr_live_••••••••" + k.KeySuffix,
                    createdAt  = k.CreatedAt,
                    lastUsedAt = k.LastUsedAt,
                })
                .ToListAsync();

            return Results.Ok(keys);
        });

        group.MapPost("/", async (CreateKeyRequest req, HttpContext ctx, AppDbContext db, UserManager<AppUser> userMgr) =>
        {
            var user = await userMgr.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Label))
                return Results.BadRequest(new { error = "Label is required" });

            var rawKey = "cr_live_" + GenerateHex(32);
            var key = new ApiKey
            {
                UserId    = user.Id,
                Label     = req.Label.Trim(),
                KeyHash   = KeyHasher.Hash(rawKey),
                KeySuffix = rawKey[^4..],
            };

            db.ApiKeys.Add(key);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                id        = key.Id,
                label     = key.Label,
                key       = rawKey,
                masked    = "cr_live_••••••••" + key.KeySuffix,
                createdAt = key.CreatedAt,
            });
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db, UserManager<AppUser> userMgr) =>
        {
            var user = await userMgr.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();

            var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.UserId == user.Id);
            if (key is null) return Results.NotFound();

            db.ApiKeys.Remove(key);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static string GenerateHex(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length / 2);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

record CreateKeyRequest(string Label);
