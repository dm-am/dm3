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
    /// Messages that exhausted every retry and left the pipeline as an exception.
    /// The next thing that happens to one is the dead-letter exchange.
    /// Attributes: <c>queue</c>, <c>reason</c> (exception type).
    /// </summary>
    public static readonly Counter<long> Failed =
        Meter.CreateCounter<long>("dm.messaging.failed", null, "Messages that exhausted every retry");

    /// <summary>
    /// Individual retry attempts. Rises long before <see cref="Failed"/> does,
    /// which is the point: a queue that is being processed on the second attempt
    /// is a queue about to stop being processed. Attributes: <c>queue</c>.
    /// </summary>
    public static readonly Counter<long> Retried =
        Meter.CreateCounter<long>("dm.messaging.retried", null, "Retry attempts spent on messages");

    /// <summary>
    /// Time one message spent in the pipeline, retries included, seconds.
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
    /// letter about a sign-in from an unknown address. A broker refusal is a delay
    /// (<see cref="OutboxPublishFailed"/>).
    /// </remarks>
    public static readonly Counter<long> PublishFailed =
        Meter.CreateCounter<long>("dm.messaging.publish_failed", null, "Events the publisher could not hand to the broker");

    /// <summary>
    /// Age of the oldest unpublished outbox row, seconds; 0 on an empty
    /// backlog. Updated by every relay pass.
    /// </summary>
    /// <remarks>
    /// The number behind the backlog alert.
    /// </remarks>
    public static readonly Gauge<double> OutboxLag =
        Meter.CreateGauge<double>("dm.messaging.outbox_lag", "s", "Age of the oldest unpublished outbox row");

    /// <summary>Unpublished outbox rows. Updated by every relay pass.</summary>
    public static readonly Gauge<long> OutboxPending =
        Meter.CreateGauge<long>("dm.messaging.outbox_pending", null, "Unpublished outbox rows");

    /// <summary>Outbox rows published with confirmation and marked.</summary>
    public static readonly Counter<long> OutboxPublished =
        Meter.CreateCounter<long>("dm.messaging.outbox_published", null, "Outbox rows published and marked");

    /// <summary>
    /// Publish refusals of the relay. Attributes: <c>reason</c> (exception
    /// type). A delay, not a loss: the row stays in the outbox and the next
    /// pass retries it — the opposite of <see cref="PublishFailed"/>.
    /// </summary>
    public static readonly Counter<long> OutboxPublishFailed =
        Meter.CreateCounter<long>("dm.messaging.outbox_publish_failed", null, "Outbox publish refusals, retried by the next pass");

    /// <summary>
    /// Relay passes, ticking on an empty backlog too.
    /// </summary>
    /// <remarks>
    /// A stalled relay would leave the lag gauge frozen; the stall alert reads this.
    /// </remarks>
    public static readonly Counter<long> OutboxRelayPasses =
        Meter.CreateCounter<long>("dm.messaging.outbox_relay_passes", null, "Passes of the outbox relay");

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

    /// <summary>Attribute set naming the channel a delivery was attempted through.</summary>
    /// <param name="channel">Channel name.</param>
    public static KeyValuePair<string, object?> Channel(string channel) => new("channel", channel);

    /// <summary>Attribute set naming the queue a measurement belongs to.</summary>
    /// <param name="queue">Queue name.</param>
    public static KeyValuePair<string, object?> Queue(string queue) => new("queue", queue);
}
