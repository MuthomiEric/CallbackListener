using System.Diagnostics;
using System.Text.Json;
using CallbackListener.Agent;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;

const string ServiceName    = "CallbackAgent";
const string ServiceDisplay = "Callback Agent";
const string ServiceDesc    = "Relays webhooks from Callback Relay server to local services.";

// ── Parse arguments ───────────────────────────────────────────────────────────
var command = "run";
var server  = "";
var apiKey  = "";
var timeout = 0;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i].ToLowerInvariant())
    {
        case "install":    command = "install";                      break;
        case "uninstall":  command = "uninstall";                    break;
        case "--server":   server  = args[++i];                      break;
        case "--api-key":  apiKey  = args[++i];                      break;
        case "--timeout":  int.TryParse(args[++i], out timeout);     break;
        case "--help":
        case "-h":
            PrintHelp();
            return 0;
    }
}

// ── Install ───────────────────────────────────────────────────────────────────
if (command == "install")
{
    if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(apiKey))
    {
        Console.Error.WriteLine("Error: --server and --api-key are required for install.");
        Console.Error.WriteLine();
        PrintHelp();
        return 1;
    }

    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    // Use a raw string so the SignalR key comes out with a dot, not underscore
    var json = $$"""
        {
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore.SignalR": "Warning"
            }
          },
          "Agent": {
            "ServerUrl": "{{server}}",
            "ApiKey": "{{apiKey}}",
            "LocalTimeoutSeconds": {{(timeout > 0 ? timeout : 30)}}
          }
        }
        """;

    File.WriteAllText(configPath, json);
    Console.WriteLine($"Config saved to {configPath}");

    if (OperatingSystem.IsWindows())
    {
        InstallWindowsService();
    }
    else if (OperatingSystem.IsLinux())
    {
        InstallSystemd();
    }
    else
    {
        Console.WriteLine("Auto-install not supported on this OS. Run the agent manually:");
        Console.WriteLine($"  {Environment.ProcessPath}");
    }

    return 0;
}

// ── Uninstall ─────────────────────────────────────────────────────────────────
if (command == "uninstall")
{
    if (OperatingSystem.IsWindows())
        UninstallWindowsService();
    else if (OperatingSystem.IsLinux())
        UninstallSystemd();
    else
        Console.Error.WriteLine("Auto-uninstall not supported on this OS.");
    return 0;
}

// ── Run (foreground or as OS service) ─────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);
builder.Environment.ContentRootPath = AppContext.BaseDirectory;

if (OperatingSystem.IsWindows())
    builder.Services.AddWindowsService(o => o.ServiceName = ServiceName);
else if (OperatingSystem.IsLinux())
    builder.Services.AddSystemd();

// CLI flags override appsettings.json (useful for one-shot foreground runs)
var overrides = new Dictionary<string, string?>();
if (!string.IsNullOrEmpty(server))  overrides[$"{AgentOptions.SectionName}:ServerUrl"]           = server;
if (!string.IsNullOrEmpty(apiKey))  overrides[$"{AgentOptions.SectionName}:ApiKey"]              = apiKey;
if (timeout > 0)                    overrides[$"{AgentOptions.SectionName}:LocalTimeoutSeconds"]  = timeout.ToString();
if (overrides.Count > 0)
    builder.Configuration.AddInMemoryCollection(overrides);

builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection(AgentOptions.SectionName));

builder.Services.AddSingleton(new HttpClient(new HttpClientHandler
{
    AllowAutoRedirect = false,
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
})
{
    DefaultRequestVersion = new Version(1, 1)
});

builder.Services.AddHostedService<RelayWorker>();

var host = builder.Build();
var log   = host.Services.GetRequiredService<ILogger<Program>>();
var cfg   = host.Services.GetRequiredService<IConfiguration>();

log.LogInformation("CallbackAgent starting — server: {Url}",
    cfg[$"{AgentOptions.SectionName}:ServerUrl"]);

await host.RunAsync();
return 0;

// ── Helpers ───────────────────────────────────────────────────────────────────
static void PrintHelp()
{
    Console.WriteLine("""
        CallbackAgent — relays webhooks to your local service

        Usage:
          CallbackAgent install --server <url> --api-key <key> [--timeout <secs>]
              Saves config and registers as a system service (auto-starts on boot).
              One agent handles ALL apps registered to that API key.

          CallbackAgent uninstall
              Stops and removes the system service.

          CallbackAgent --server <url> --api-key <key>
              Runs in the foreground (useful for testing).

          CallbackAgent
              Runs using saved appsettings.json (used by the system service).

        Options:
          --server   <url>   Relay server URL  (e.g. https://callback.example.com)
          --api-key  <key>   API key from the dashboard
          --timeout  <secs>  Local forward timeout in seconds (default: 30)
        """);
}

static void InstallWindowsService()
{
    var exe = Environment.ProcessPath!;

    Console.WriteLine("Installing Windows service…");
    Run("sc", $"create {ServiceName} binPath= \"{exe}\" start= auto DisplayName= \"{ServiceDisplay}\"");
    Run("sc", $"description {ServiceName} \"{ServiceDesc}\"");
    // restart after 5s, 10s, 30s on consecutive failures; reset counter after 1 day
    Run("sc", $"failure {ServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000");
    Run("sc", $"start {ServiceName}");
    Console.WriteLine($"Service '{ServiceName}' installed and started. It will auto-start on every boot.");
    Console.WriteLine($"  sc stop   {ServiceName}   — to stop");
    Console.WriteLine($"  sc start  {ServiceName}   — to start");
    Console.WriteLine($"  CallbackAgent uninstall    — to remove");
}

static void UninstallWindowsService()
{
    Console.WriteLine("Removing Windows service…");
    Run("sc", $"stop   {ServiceName}");
    Run("sc", $"delete {ServiceName}");
    Console.WriteLine($"Service '{ServiceName}' removed.");
}

static void InstallSystemd()
{
    var exe  = Environment.ProcessPath!;
    var unit = $"""
        [Unit]
        Description={ServiceDesc}
        After=network-online.target
        Wants=network-online.target

        [Service]
        Type=notify
        ExecStart={exe}
        Restart=on-failure
        RestartSec=5
        TimeoutStartSec=30

        [Install]
        WantedBy=multi-user.target
        """;

    const string unitPath = "/etc/systemd/system/callback-agent.service";
    File.WriteAllText(unitPath, unit);
    Run("systemctl", "daemon-reload");
    Run("systemctl", "enable callback-agent");
    Run("systemctl", "start callback-agent");
    Console.WriteLine("Service installed and started. It will auto-start on every boot.");
    Console.WriteLine("  systemctl stop    callback-agent   — to stop");
    Console.WriteLine("  systemctl start   callback-agent   — to start");
    Console.WriteLine("  CallbackAgent uninstall             — to remove");
}

static void UninstallSystemd()
{
    const string unitPath = "/etc/systemd/system/callback-agent.service";
    Run("systemctl", "stop callback-agent");
    Run("systemctl", "disable callback-agent");
    if (File.Exists(unitPath)) File.Delete(unitPath);
    Run("systemctl", "daemon-reload");
    Console.WriteLine("Service removed.");
}

static void Run(string file, string arguments)
{
    var psi = new ProcessStartInfo(file, arguments)
    {
        UseShellExecute        = false,
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
    };
    using var proc = Process.Start(psi)!;
    var stdout = proc.StandardOutput.ReadToEnd().Trim();
    var stderr = proc.StandardError.ReadToEnd().Trim();
    proc.WaitForExit();
    if (!string.IsNullOrEmpty(stdout)) Console.WriteLine(stdout);
    if (!string.IsNullOrEmpty(stderr)) Console.Error.WriteLine(stderr);
}
