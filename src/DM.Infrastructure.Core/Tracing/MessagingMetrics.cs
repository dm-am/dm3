using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Metrics of message consumption: only the consumer says whether the work it took
/// succeeded.
/// </summary>
/// <remarks>
/// The unit only in the unit argument and none on a counter: the Prometheus
/// exporter appends it to the name, verbatim outside UCUM.
/// </remarks>
public static class MessagingMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Messaging";

    private static readonly Meter Meter = new(MeterName, DmActivitySource.Version);

    /// <summary>
    /// Messages a consumer finished with. Attributes: <c>queue</c>,
    /// <c>result</c> (the processing result the pipeline returned).
    /// </summary>
    public static readonly Counter<long> Consumed =
        Meter.CreateCounter<long>("dm.messaging.consumed", null, "Messages a consumer finished with");

    /// <summary>
    /// Outbox rows that became dead: the last failure of the retry ladder, or a rejection.
    /// Nothing claims one again until a human returns it.
    /// Attributes: <c>queue</c>, <c>reason</c> (exception type, or <c>Rejected</c>).
    /// </summary>
    public static readonly Counter<long> Failed =
        Meter.CreateCounter<long>("dm.messaging.failed", null, "Messages that exhausted every retry");

    /// <summary>
    /// Pauses of the retry ladder assigned to failed rows. Rises long before
    /// <see cref="Failed"/> does, which is the point: a queue whose rows fail on
    /// the first attempt is a queue about to fill with dead rows. Attributes: <c>queue</c>.
    /// </summary>
    public static readonly Counter<long> Retried =
        Meter.CreateCounter<long>("dm.messaging.retried", null, "Retry attempts spent on messages");

    /// <summary>
    /// Time the handler spent on one claimed row, seconds.
    /// Attributes: <c>queue</c>.
    /// </summary>
    public static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("dm.messaging.duration", "s", "End-to-end message processing latency");

    /// <summary>
    /// Events lost for good: nothing stored them and nothing will retry them.
    /// Attributes: <c>event</c> (event type), <c>reason</c> (exception type).
    /// </summary>
    /// <remarks>
    /// The producer swallows a refusal rather than fail a committed write, so this is
    /// what shows it: the outbox insert refused after the commit, and the swallowed
    /// letter about a sign-in from an unknown address.
    /// </remarks>
    public static readonly Counter<long> PublishFailed =
        Meter.CreateCounter<long>("dm.messaging.publish_failed", null, "Events and letters that could not be stored");

    /// <summary>
    /// Notifications that reached their channel and were refused by it.
    /// Attributes: <c>channel</c> (email, discord, telegram, realtime),
    /// <c>event</c> (notification type).
    /// </summary>
    /// <remarks>
    /// A sender swallows what the channel answered, so the consumer counters see a
    /// success. Nothing about the recipient, which would open a series per user.
    /// </remarks>
    public static readonly Counter<long> DeliveryFailed = Meter.CreateCounter<long>(
        "dm.messaging.delivery_failed", null, "Notifications a channel refused to deliver");

    /// <summary>
    /// Whether the push listener of this API instance is listening: 1 or 0.
    /// </summary>
    /// <remarks>
    /// Recorded on every change of the listener's state and 0 from its start, so an instance
    /// that never reached its database reads 0 and not nothing. The push alert reads it.
    /// </remarks>
    public static readonly Gauge<long> PushListening =
        Meter.CreateGauge<long>("dm.messaging.push_listening", null, "Whether the push listener of this instance is listening");

    /// <summary>Attribute set naming the channel a delivery was attempted through.</summary>
    /// <param name="channel">Channel name.</param>
    public static KeyValuePair<string, object?> Channel(string channel) => new("channel", channel);

    /// <summary>Attribute set naming the queue a measurement belongs to.</summary>
    /// <param name="queue">Queue name.</param>
    public static KeyValuePair<string, object?> Queue(string queue) => new("queue", queue);
}
