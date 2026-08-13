using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Metrics of message consumption (Prometheus + OTel). Global SSOT — everything
/// the two workers publish about the messages they handle lives here.
/// </summary>
/// <remarks>
/// The broker says how much work is waiting; only the consumer says whether the
/// work it took succeeded. Before these, a worker that failed every message it
/// touched looked identical to an idle one from the outside: its process
/// answered, its request rate was the scrape interval, its heap was flat, and
/// the only trace of the failures was a warning line nothing was watching.
///
/// Names follow OpenTelemetry semantic conventions: snake_case, dot-separated
/// namespace, and the unit only in the unit argument, because the Prometheus
/// exporter appends its own suffix on top of whatever the name already says.
/// The counters carry no unit at all: a unit outside the UCUM table is appended
/// verbatim, so "messages" would export dm_messaging_failed_messages_total and
/// every rule written from this file would query a series that does not exist.
/// What the scrape shows is dm_messaging_{consumed,failed,retried}_total and
/// dm_messaging_duration_seconds.
/// </remarks>
public static class MessagingMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Messaging";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

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
    /// Events the publisher could not hand to the broker. Attributes:
    /// <c>event</c> (event type), <c>reason</c> (exception type).
    /// </summary>
    /// <remarks>
    /// An event is not the carrier of the fact — SYSTEM.md says so, and the
    /// publisher therefore swallows a refusal instead of turning a committed
    /// write into a 500. That trade only holds while someone can see the
    /// swallowing: without this counter a broker that refuses every publish looks
    /// exactly like a quiet site, and the first symptom is a user asking why
    /// notifications stopped.
    ///
    /// Not only the bus. The one letter whose loss a caller swallows on the same
    /// terms — the warning about a login from an unknown address — counts here
    /// too, so <c>event</c> names what failed to leave rather than which pipe it
    /// was leaving through.
    /// </remarks>
    public static readonly Counter<long> PublishFailed =
        Meter.CreateCounter<long>("dm.messaging.publish_failed", null, "Events the publisher could not hand to the broker");

    /// <summary>
    /// Notifications that reached their channel and were refused by it.
    /// Attributes: <c>channel</c> (email, discord, telegram, realtime),
    /// <c>event</c> (notification type).
    /// </summary>
    /// <remarks>
    /// The far end of the same trade as <see cref="PublishFailed"/>, and the one
    /// that was invisible. A message is taken off the queue, handed to a sender,
    /// and the sender swallows what the channel answered: a bot that returns 403
    /// for every chat, a relay that refuses every letter and a push nobody is
    /// subscribed to are all a warning line and an acknowledged message. The
    /// consumer counters say the message was consumed successfully, because from
    /// the pipeline's side it was — the swallowing happens below it.
    ///
    /// The channel and the type of notification, and nothing about the recipient:
    /// the address, the chat and the user are what makes a failure specific, and
    /// a label carrying any of them multiplies streams per user without bound.
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
