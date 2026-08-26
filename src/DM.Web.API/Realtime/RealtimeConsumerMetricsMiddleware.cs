using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Messaging;

namespace DM.Web.API.Realtime;

/// <summary>
/// Counts the realtime notifications this host takes off the broker.
/// </summary>
/// <remarks>
/// Measured and deliberately not retried, the one middleware of the three that
/// leaves the policy out. Its message is a copy of a notification the dispatcher
/// has already stored, the queue is consumed sequentially, and a schedule that
/// spends up to a minute on one delivery would hold every later push behind a
/// message nobody can act on any more. The same decision keeps this queue out of
/// the dead-letter routing, where DeadLetterRoutingShould names it.
///
/// The counters are the half that does apply. Without them a host whose push
/// fails on every message answers its health probe, keeps its subscription and
/// draws the flat lines of a quiet evening - the state the whole set of
/// instruments exists to tell apart.
/// </remarks>
internal class RealtimeConsumerMetricsMiddleware : IConsumerMiddleware
{
    private readonly MeasuredConsumerPipeline _pipeline =
        MeasuredConsumerPipeline.Measuring(RealtimeNotificationConsumer.QueueName);

    /// <inheritdoc />
    public Task<ProcessResult> InvokeAsync(
        ConsumerContext context,
        ConsumerDelegate next,
        CancellationToken cancellationToken) =>
        _pipeline.InvokeAsync(context, next, cancellationToken);
}
