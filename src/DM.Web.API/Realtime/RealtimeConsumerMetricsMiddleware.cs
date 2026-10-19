using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Messaging;

namespace DM.Web.API.Realtime;

/// <summary>
/// Counts the realtime notifications this host takes off the broker.
/// </summary>
/// <remarks>
/// Measured and not retried: the queue is sequential, and a retry schedule would
/// hold every later push behind one nobody can act on any more. Without the
/// counters a push failing on every message looks like a quiet evening.
/// </remarks>
internal class RealtimeConsumerMetricsMiddleware : IConsumerMiddleware
{
    private readonly MeasuredConsumerPipeline _pipeline =
        MeasuredConsumerPipeline.Measuring(RealtimeNotificationConsumer.QueueName);

    /// <inheritdoc />
    public Task<ProcessResult> InvokeAsync(ConsumerDelegate next, CancellationToken cancellationToken) =>
        _pipeline.InvokeAsync(next, cancellationToken);
}
