namespace CallbackListener.Domain;

public sealed class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;
    public Guid? ListenerId { get; set; }
    public Listener? Listener { get; set; }
    public string Label { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string KeySuffix { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
}
