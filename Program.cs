using System.Text.Json;
using System.Threading.RateLimiting;
using CallbackListener.Application.Interfaces;
using CallbackListener.Application.Services;
using CallbackListener.Infrastructure.Services;
using CallbackListener.Configuration;
using CallbackListener.Domain;
using CallbackListener.Infrastructure.Data;
using CallbackListener.Infrastructure.Hubs;
using CallbackListener.Web.Endpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
};

var builder = WebApplication.CreateBuilder(options);

// ── Host ──────────────────────────────────────────────────────────────────────
if (OperatingSystem.IsWindows())
    builder.Host.UseWindowsService();
else if (OperatingSystem.IsLinux())
    builder.Host.UseSystemd();

builder.WebHost.UseUrls("http://0.0.0.0:5055");

// ── Configuration ─────────────────────────────────────────────────────────────
var section = builder.Configuration.GetSection(AppOptions.SectionName);
builder.Services.Configure<AppOptions>(section);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services
    .AddIdentityCore<AppUser>(opt =>
    {
        opt.Password.RequireDigit           = false;
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequiredLength         = 8;
        opt.Password.RequireUppercase       = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

// ── Authentication ────────────────────────────────────────────────────────────
var authBuilder = builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme);

authBuilder.AddIdentityCookies();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
if (!string.IsNullOrEmpty(googleClientId))
{
    authBuilder.AddGoogle(opt =>
    {
        opt.ClientId     = googleClientId;
        opt.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        // Must match the redirect URI registered in Google Cloud Console exactly
        opt.CallbackPath = "/auth/google/callback";
        // Without this, the OAuth handler uses DefaultSignInScheme (application cookie)
        // instead of the external cookie — GetExternalLoginInfoAsync() then returns null.
        opt.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;
    });
}

builder.Services.AddAuthorization();

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<ICallbackStore, CallbackStore>();
builder.Services.AddSingleton<ICallbackCounter, CallbackCounter>();
builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();
builder.Services.AddSingleton<ICallbackService, CallbackService>();
builder.Services.AddHostedService<CallbackFlushService>();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services
    .AddSignalR(o => { o.MaximumReceiveMessageSize = 512 * 1024; })
    .AddJsonProtocol(o =>
    {
        o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.PayloadSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// ── Rate Limiting ─────────────────────────────────────────────────────────────
var rateLimitPerMinute = section.GetValue<int>(nameof(AppOptions.RateLimitPerMinute), 120);

builder.Services.AddMemoryCache();

builder.Services.AddRateLimiter(rl =>
{
    rl.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window      = TimeSpan.FromMinutes(1),
                PermitLimit = rateLimitPerMinute,
                QueueLimit  = 0
            }));

    // Per-slug limit for callback ingestion: 60 req/min per unique slug.
    rl.AddPolicy("per-slug", ctx =>
    {
        var slug = ctx.Request.Query["slug"].FirstOrDefault() ?? "__none__";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"slug:{slug}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window      = TimeSpan.FromMinutes(1),
                PermitLimit = 60,
                QueueLimit  = 0
            });
    });

    rl.RejectionStatusCode = 429;
    rl.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded." });
    };
});

// ── JSON for minimal API responses ────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── DB init ───────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ── Middleware Pipeline ───────────────────────────────────────────────────────
// Trust X-Forwarded-Proto from Cloudflare Tunnel so OAuth redirects use https://
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});
app.UseRateLimiter();
app.UseDefaultFiles();

// Rewrite extension-less paths to .html so /account/apps works like /account/apps.html
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (!Path.HasExtension(path) &&
        !path.StartsWith("/api/",     StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/auth/",    StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/hubs/",    StringComparison.OrdinalIgnoreCase))
    {
        var candidate = path.TrimEnd('/') + ".html";
        var file = app.Environment.WebRootFileProvider.GetFileInfo(candidate);
        if (file.Exists)
            ctx.Request.Path = candidate;
    }
    await next(ctx);
});

var mimeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
mimeProvider.Mappings[".sh"]  = "text/plain";
mimeProvider.Mappings[".ps1"] = "text/plain";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider  = mimeProvider,
    ServeUnknownFileTypes = true,          // allows extension-less binaries in /downloads/
    DefaultContentType   = "application/octet-stream",
});   // serves wwwroot files before routing touches anything
app.UseRouting();       // explicit placement so catch-all never races with static files
app.UseAuthentication();
app.UseAuthorization();

// ── Hubs ──────────────────────────────────────────────────────────────────────
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapHub<AgentHub>("/hubs/agents");

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapListenerEndpoints();
app.MapClientEndpoints();
app.MapCallbackEndpoints();
app.MapApiEndpoints();
app.MapAdminEndpoints();

// ── Lifecycle ─────────────────────────────────────────────────────────────────
app.Lifetime.ApplicationStarted.Register(() =>
    app.Logger.LogInformation("CallbackListener started — http://0.0.0.0:5055"));

app.Lifetime.ApplicationStopping.Register(() =>
    app.Logger.LogInformation("CallbackListener stopping..."));

app.Run();
