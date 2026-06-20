namespace CallbackListener.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "CallbackListener";

    public int MaxBodySizeBytes { get; init; } = 1_048_576;
    public int MaxHistoryCount { get; init; } = 200;
    public int RateLimitPerMinute { get; init; } = 120;
    public string AdminEmail { get; init; } = string.Empty;

    /// <summary>Seconds to wait for the agent to confirm local delivery before marking Dropped.</summary>
    public int LocalDeliveryTimeoutSeconds { get; init; } = 35;
}
