namespace CallbackListener.Domain;

public sealed class VisitorIp
{
    public string IpHash      { get; set; } = string.Empty;
    public DateTimeOffset FirstSeenAt { get; set; }
}
