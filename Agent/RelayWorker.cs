using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace CallbackListener.Agent;

public sealed class RelayWorker : BackgroundService
{
    // Headers that must not be forwarded to the local service
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade",
        "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailers",
        "Host", "Content-Length" // HttpClient handles these
    };

    private readonly AgentOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<RelayWorker> _logger;

    public RelayWorker(
        IOptions<AgentOptions> options,
        HttpClient http,
        ILogger<RelayWorker> logger)
    {
        _options = options.Value;
        _http = http;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ServerUrl))
        {
            _logger.LogCritical("Agent:ServerUrl is not configured. Run with --help to see usage.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey == "REPLACE_WITH_YOUR_API_KEY")
        {
            _logger.LogCritical("Agent:ApiKey is not configured. Run 'CallbackAgent install --help' to see usage.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_options.Slug))
        {
            _logger.LogCritical("Agent:Slug is not configured. Run 'CallbackAgent install --help' to see usage.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var connection = BuildConnection();

            connection.On<RelayEntry>("CallbackReceived", entry =>
                _ = ForwardAsync(entry, stoppingToken));

            try
            {
                // Signal when the connection is closed so we can reconnect
                var closedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.Closed += _ => { closedTcs.TrySetResult(); return Task.CompletedTask; };

                await connection.StartAsync(stoppingToken);
                _logger.LogInformation("Connected to relay server at {Url}", _options.ServerUrl);

                // Block until the connection drops or cancellation is requested
                await closedTcs.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection error — retrying in 5s");
            }
            finally
            {
                await connection.DisposeAsync();
            }

            if (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }

    private HubConnection BuildConnection()
    {
        var hubUrl = $"{_options.ServerUrl.TrimEnd('/')}/hubs/agents?apiKey={Uri.EscapeDataString(_options.ApiKey)}&slug={Uri.EscapeDataString(_options.Slug)}";

        return new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new RetryPolicy())
            .AddJsonProtocol(o =>
            {
                o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();
    }

    private async Task ForwardAsync(RelayEntry entry, CancellationToken ct)
    {
        if (entry.Relay is null)
        {
            _logger.LogDebug("Callback {Id} has no relay config — ignoring (display-only)", entry.Id);
            return;
        }

        var localUrl = BuildLocalUrl(entry);

        _logger.LogInformation(
            "← [{Slug}] {Method} {SubPath}  →  {LocalUrl}",
            entry.Slug, entry.Method, entry.SubPath, localUrl);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.LocalTimeoutSeconds));

            using var request = BuildRequest(entry, localUrl);
            using var response = await _http.SendAsync(request, timeout.Token);

            _logger.LogInformation(
                "✓ [{Slug}] {Method} {SubPath}  →  {Status}",
                entry.Slug, entry.Method, entry.SubPath, (int)response.StatusCode);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "✗ [{Slug}] {Method} {SubPath}  — local service timed out after {Sec}s",
                entry.Slug, entry.Method, entry.SubPath, _options.LocalTimeoutSeconds);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "✗ [{Slug}] {Method} {SubPath}  — could not reach {LocalUrl}",
                entry.Slug, entry.Method, entry.SubPath, localUrl);
        }
    }

    private static string BuildLocalUrl(RelayEntry entry)
    {
        var base_ = entry.Relay!.BasePath.TrimEnd('/');
        var sub   = entry.SubPath.TrimStart('/');
        var path  = string.IsNullOrEmpty(sub) ? base_ + "/" : base_ + "/" + sub;

        var query = entry.Query.Count > 0
            ? "?" + string.Join("&", entry.Query.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"))
            : string.Empty;

        return $"{entry.Relay.Scheme}://localhost:{entry.Relay.Port}{path}{query}";
    }

    private static HttpRequestMessage BuildRequest(RelayEntry entry, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(entry.Method), url);

        // Forward headers, skipping hop-by-hop and system headers
        foreach (var (key, value) in entry.Headers)
        {
            if (HopByHopHeaders.Contains(key)) continue;
            request.Headers.TryAddWithoutValidation(key, value);
        }

        if (!string.IsNullOrEmpty(entry.RawBody))
        {
            var content = new StringContent(entry.RawBody, Encoding.UTF8);
            if (!string.IsNullOrEmpty(entry.ContentType))
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(entry.ContentType);
            request.Content = content;
        }

        return request;
    }

    // Exponential back-off: 2s, 5s, 10s, 30s, then 30s forever
    private sealed class RetryPolicy : IRetryPolicy
    {
        private static readonly TimeSpan[] Delays =
            [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5),
             TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

        public TimeSpan? NextRetryDelay(RetryContext ctx) =>
            Delays[Math.Min(ctx.PreviousRetryCount, Delays.Length - 1)];
    }
}
