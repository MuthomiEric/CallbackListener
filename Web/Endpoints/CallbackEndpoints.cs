using System.Text.Json;
using CallbackListener.Application.Interfaces;
using CallbackListener.Configuration;
using CallbackListener.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CallbackListener.Web.Endpoints;

public static class CallbackEndpoints
{
    public static void MapCallbackEndpoints(this WebApplication app)
    {
        // Catch-all: any path on the domain is a potential webhook.
        // The target app is identified by ?slug=<value> in the query string.
        // Example: POST https://relay.example.com/payment/stripe?slug=x6tYt
        app.Map("/{**subpath}", HandleAsync).RequireRateLimiting("per-slug");
    }

    private static async Task<IResult> HandleAsync(
        HttpContext ctx,
        ICallbackService callbackService,
        AppDbContext db,
        IOptions<AppOptions> options,
        string? subpath = null,
        CancellationToken ct = default)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (path.StartsWith("/api/",     StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/auth/",    StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hubs/",    StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/account/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/",      StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/favicon.ico",  StringComparison.OrdinalIgnoreCase))
            return Results.NotFound();

        var slug = ctx.Request.Query["slug"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(slug))
            return Results.BadRequest(new { error = "Missing required query parameter: slug" });

        var exists = await db.Listeners.AnyAsync(l => l.Slug == slug, ct);
        if (!exists)
            return Results.NotFound(new { error = $"No app registered for slug '{slug}'" });

        if (ctx.Request.ContentLength > options.Value.MaxBodySizeBytes)
            return Results.StatusCode(413);

        var rawBody = await ReadBodyAsync(ctx.Request, ct);
        var isJson  = TryDetectJson(rawBody);

        // Forward all headers except pseudo-headers; redact Authorization value.
        var safeHeaders = ctx.Request.Headers
            .Where(h => !h.Key.StartsWith(':'))
            .ToDictionary(
                h => h.Key,
                h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                    ? "[redacted]"
                    : h.Value.ToString());

        // Exclude the routing slug from the forwarded query params.
        var query = ctx.Request.Query
            .Where(q => !q.Key.Equals("slug", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(q => q.Key, q => q.Value.ToString());

        var callbackCtx = new CallbackContext(
            Slug:        slug,
            SubPath:     NormalizePath(subpath),
            Method:      ctx.Request.Method,
            SourceIp:    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            ContentType: ctx.Request.ContentType ?? string.Empty,
            Headers:     safeHeaders,
            Query:       query,
            RawBody:     rawBody,
            IsJsonBody:  isJson
        );

        var entry = await callbackService.ProcessAsync(callbackCtx, ct);

        return Results.Accepted(value: new
        {
            id     = entry.Id,
            status = entry.Status.ToString(),
            detail = entry.StatusDetail
        });
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    private static bool TryDetectJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;
        try { JsonSerializer.Deserialize<JsonElement>(body); return true; }
        catch { return false; }
    }

    private static string NormalizePath(string? subpath) =>
        string.IsNullOrEmpty(subpath) ? "/" : $"/{subpath.TrimStart('/')}";
}
