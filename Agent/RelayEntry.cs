namespace CallbackListener.Agent;

/// <summary>
/// Mirrors CallbackEntry from the server — only the fields the agent needs.
/// </summary>
public sealed class RelayEntry
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string SubPath { get; init; } = "/";
    public string Method { get; init; } = "POST";
    public string ContentType { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = [];
    public Dictionary<string, string> Query { get; init; } = [];
    public string RawBody { get; init; } = string.Empty;
    public RelayTargetDto? Relay { get; init; }
}

public sealed class RelayTargetDto
{
    public string Scheme { get; init; } = "http";
    public int Port { get; init; } = 80;
    public string BasePath { get; init; } = "/";
}
