using CallbackListener.Agent;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = Host.CreateApplicationBuilder(args);
builder.Environment.ContentRootPath = AppContext.BaseDirectory;

if (OperatingSystem.IsWindows())
    builder.Services.AddWindowsService(o => o.ServiceName = "CallbackAgent");
else if (OperatingSystem.IsLinux())
    builder.Services.AddSystemd();

builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection(AgentOptions.SectionName));

// Singleton HttpClient for forwarding to the local service.
// - No auto-redirect: let the local service handle redirects itself.
// - Accepts any certificate: dev local services often use self-signed certs.
builder.Services.AddSingleton(new HttpClient(new HttpClientHandler
{
    AllowAutoRedirect = false,
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
})
{
    DefaultRequestVersion = new Version(1, 1)
});

builder.Services.AddHostedService<RelayWorker>();

var host = builder.Build();

var log = host.Services.GetRequiredService<ILogger<Program>>();

var cfg = host.Services.GetRequiredService<IConfiguration>();
var serverUrl = cfg[$"{AgentOptions.SectionName}:ServerUrl"];
log.LogInformation("CallbackAgent starting — relay server: {Url}", serverUrl);

await host.RunAsync();
