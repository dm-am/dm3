using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Tracing;
using Microsoft.Extensions.Logging;
using Polly.Retry;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// The retry policy of a consumer and the counters of the queue it reads, in one
/// place for every host that consumes.
/// </summary>
/// <remarks>
/// A helper rather than a base class: the middlewares share the policy and the instruments, not an identity.
/// </remarks>
public sealed class MeasuredConsumerPipeline
{
    private readonly string _queue;
    private readonly AsyncRetryPolicy? _retryPolicy;

    private MeasuredConsumerPipeline(string queue, AsyncRetryPolicy? retryPolicy)
    {
        _queue = queue;
        _retryPolicy = retryPolicy;
    }

    /// <summary>
    /// Measures a queue whose failures are worth another attempt.
    /// </summary>
    /// <param name="queue">Queue name, as the metrics label it.</param>
    /// <param name="logger">Logger of the middleware this sits behind.</param>
    public static MeasuredConsumerPipeline Retrying(string queue, ILogger logger) =>
        new(queue, BrokerRetry.Create(
            exception =>
            {
                MessagingMetrics.Retried.Add(1, MessagingMetrics.Queue(queue));
                logger.LogWarning(exception, "Retrying a message of {Queue}", queue);
            },
            exception => exception is not OperationCanceledException));

    /// <summary>
    /// Measures a queue whose message is not worth a second attempt.
    /// </summary>
    /// <param name="queue">Queue name, as the metrics label it.</param>
    public static MeasuredConsumerPipeline Measuring(string queue) => new(queue, null);

    /// <summary>
    /// Runs the rest of the pipeline under the policy of this queue and measures it.
    /// </summary>
    /// <param name="next">Rest of the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ProcessResult> InvokeAsync(
        ConsumerDelegate next,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            // Cancellation is not retried: a stopping host leaves the message to the closing connection.
            var result = _retryPolicy is null
                ? await next.Invoke(cancellationToken)
                : await _retryPolicy.ExecuteAsync(() => next.Invoke(cancellationToken));
            MessagingMetrics.Consumed.Add(1,
                MessagingMetrics.Queue(_queue),
                new KeyValuePair<string, object?>("result", result.ToString()));
            return result;
        }
        catch (Exception exception)
        {
            MessagingMetrics.Failed.Add(1,
                MessagingMetrics.Queue(_queue),
                new KeyValuePair<string, object?>("reason", exception.GetType().Name));
            throw;
        }
        finally
        {
            MessagingMetrics.Duration.Record(
                Stopwatch.GetElapsedTime(started).TotalSeconds, MessagingMetrics.Queue(_queue));
        }
    }
}
