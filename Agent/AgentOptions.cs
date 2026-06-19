namespace CallbackListener.Agent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Base URL of the relay server. E.g. https://callback.erickmuthomi.dev</summary>
    public string ServerUrl { get; init; } = string.Empty;

    /// <summary>API key issued by the relay server.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>The listener slug this agent handles (must match a slug registered on the server).</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// How long to wait for the local service to respond before timing out.
    /// Defaults to 30 seconds.
    /// </summary>
    public int LocalTimeoutSeconds { get; init; } = 30;
}
