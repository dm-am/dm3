using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Tracing;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DM.Workers.Mail;

internal class ConsumerRetryMiddleware : IConsumerMiddleware
{
    /// <summary>Queue this middleware sits in front of, as the metrics label it.</summary>
    private const string Queue = "dm.mail.sending";

    private readonly AsyncRetryPolicy _retryPolicy;

    public ConsumerRetryMiddleware(
        ILogger<ConsumerRetryMiddleware> logger)
    {
        _retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) =>
            {
                MessagingMetrics.Retried.Add(1, MessagingMetrics.Queue(Queue));
                logger.LogWarning(exception, "Something is wrong with mail sending");
            });
    }

    /// <summary>
    /// Runs the rest of the pipeline under the retry policy and measures it.
    /// </summary>
    /// <remarks>
    /// Every letter passes through here, which is why the counters are written
    /// here: the warning below was the only trace a failing sender left, and it
    /// reached no dashboard and no rule.
    /// </remarks>
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
            var result = await _retryPolicy.ExecuteAsync(() => next.Invoke(context, cancellationToken));
            MessagingMetrics.Consumed.Add(1,
                MessagingMetrics.Queue(Queue),
                new KeyValuePair<string, object?>("result", result.ToString()));
            return result;
        }
        catch (Exception exception)
        {
            MessagingMetrics.Failed.Add(1,
                MessagingMetrics.Queue(Queue),
                new KeyValuePair<string, object?>("reason", exception.GetType().Name));
            throw;
        }
        finally
        {
            MessagingMetrics.Duration.Record(
                Stopwatch.GetElapsedTime(started).TotalSeconds, MessagingMetrics.Queue(Queue));
        }
    }
}
