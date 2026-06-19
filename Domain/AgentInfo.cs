namespace CallbackListener.Domain;

public sealed record AgentInfo
{
    public string ClientId { get; init; } = string.Empty;
    public string ConnectionId { get; init; } = string.Empty;
    public DateTimeOffset ConnectedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsOnline { get; set; } = true;
}
