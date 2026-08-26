using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Tracing;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// The retry policy of a consumer and the counters of the queue it reads, in one
/// place for every host that consumes.
/// </summary>
/// <remarks>
/// Every message a consumer takes passes through its middleware, which is why the
/// counters are written there: before them, a worker that failed everything it was
/// handed looked exactly like an idle one, and the only trace of the failures was
/// a warning line nothing watched.
///
/// The two workers carried a copy each of the same file, policy and instruments
/// included, so the number of attempts and the shape of the backoff had to be
/// changed in as many places as there are consumers; and the third consumer, the
/// realtime push of the API, had neither. Both halves of that are gone: the policy
/// and the instruments live here, and the workers consume through one
/// <see cref="RetryingConsumerMiddleware"/>, which differs between them by the queue
/// it is given and by nothing else. The API keeps a middleware of its own because it
/// measures without retrying, which is another pipeline rather than another copy.
///
/// A helper the middleware calls rather than a base class it derives from: what
/// the two pipelines share is the policy and the instruments, not an identity,
/// and a base class would invite the next middleware to inherit behaviour it
/// only meant to reuse.
/// </remarks>
public sealed class MeasuredConsumerPipeline
{
    // Attempts a message gets before it leaves the pipeline as an exception. What
    // happens to it next is the queue's business: where the consumer declared a
    // dead-letter exchange the broker moves it there, and where it did not the
    // message is dropped.
    private const int Attempts = 5;

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
        new(queue, Policy.Handle<Exception>(exception =>
                exception is not OperationCanceledException)
            .WaitAndRetryAsync(Attempts,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) =>
            {
                MessagingMetrics.Retried.Add(1, MessagingMetrics.Queue(queue));
                logger.LogWarning(exception, "Retrying a message of {Queue}", queue);
            }));

    /// <summary>
    /// Measures a queue whose message is not worth a second attempt.
    /// </summary>
    /// <param name="queue">Queue name, as the metrics label it.</param>
    public static MeasuredConsumerPipeline Measuring(string queue) => new(queue, null);

    /// <summary>
    /// Runs the rest of the pipeline under the policy of this queue and measures it.
    /// </summary>
    /// <param name="context">Consumer context.</param>
    /// <param name="next">Rest of the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ProcessResult> InvokeAsync(
        ConsumerContext context,
        ConsumerDelegate next,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            // The token goes to the attempt and not to the policy. Cancellation
            // is the one exception the policy does not retry (review of W1.2): a
            // stopping host used to sit through the whole 2+4+8+16+32s ladder
            // re-asking a caller that had already left, with five false
            // dm_messaging_retried increments per message in flight. An
            // OperationCanceledException escapes on the first attempt instead,
            // and the consumer hands the message back by NOT acknowledging it -
            // the closing connection requeues it. Every other exception keeps
            // the ladder. Where a message interrupted by a stop belongs beyond
            // that is part of deciding how a consumer drains, and a full drain
            // is still not decided in this file.
            var result = _retryPolicy is null
                ? await next.Invoke(context, cancellationToken)
                : await _retryPolicy.ExecuteAsync(() => next.Invoke(context, cancellationToken));
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
