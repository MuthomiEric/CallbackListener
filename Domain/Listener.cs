namespace CallbackListener.Domain;

public sealed class Listener
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;
    public string Slug { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Scheme { get; set; } = "http";
    public int Port { get; set; } = 80;
    public string BasePath { get; set; } = "/";
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public DeliveryMode Mode { get; set; } = DeliveryMode.Both;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
