namespace DM.Infrastructure.Messaging.Outbox;

/// <summary>
/// Configuration for outbox event processing
/// </summary>
public class OutboxConfiguration
{
    /// <summary>
    /// Polling interval in seconds (default: 5)
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Maximum number of retries before dead-lettering (default: 5)
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Base retry delay in seconds for exponential backoff (default: 30)
    /// First retry: 30s, second: 60s, third: 120s, fourth: 240s, fifth: 480s
    /// </summary>
    public int BaseRetryDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of events to process in a single batch (default: 100)
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;
}
