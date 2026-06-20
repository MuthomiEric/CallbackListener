namespace CallbackListener.Domain;

public sealed record CallbackEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = string.Empty;

    /// <summary>The slug that identifies the registered listener.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>The sub-path after the slug, forwarded to the local service.</summary>
    public string SubPath { get; init; } = "/";

    public string Method { get; init; } = string.Empty;
    public string SourceIp { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = [];
    public Dictionary<string, string> Query { get; init; } = [];
    public string RawBody { get; init; } = string.Empty;
    public bool IsJsonBody { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public CallbackStatus Status { get; init; } = CallbackStatus.Received;
    public string? StatusDetail { get; init; }

    /// <summary>
    /// Relay config embedded by the server so the agent knows where to forward.
    /// Null when there is no listener registered for this slug (display-only).
    /// </summary>
    public RelayTarget? Relay { get; init; }
}

/// <summary>
/// Forwarding target attached by the server from the registered listener config.
/// The agent constructs: {Scheme}://localhost:{Port}{BasePath}{SubPath}
/// </summary>
public sealed record RelayTarget(string Scheme, int Port, string BasePath, string? ClientId = null);

public enum CallbackStatus
{
    Received,   // no listener registered — web only, no agent involved
    Routed,     // dispatched to agent — local delivery in-flight
    Delivered,  // agent confirmed local service responded
    Dropped     // agent could not reach local service
}
