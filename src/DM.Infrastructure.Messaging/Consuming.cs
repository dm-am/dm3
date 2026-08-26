using System.Threading;
using System.Threading.Tasks;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// What became of one delivered message.
/// </summary>
/// <remarks>
/// Two outcomes on purpose. The consumer acknowledges Success and rejects
/// Failure without requeue, which hands the message to the dead-letter exchange
/// where the queue names one and drops it where it does not. There is no Retry
/// member: a message worth another attempt is retried inside the pipeline by
/// <see cref="RetryingConsumerMiddleware"/>, where the attempts have a ceiling —
/// a broker-side requeue has none, and an unprocessable message would return
/// forever.
/// </remarks>
public enum ProcessResult
{
    /// <summary>The message is done with and is acknowledged.</summary>
    Success,

    /// <summary>The message cannot be processed and is rejected without requeue.</summary>
    Failure,
}

/// <summary>
/// Handler of the messages one queue delivers.
/// </summary>
/// <remarks>
/// Resolved from a scope opened for the one message and released with it, so a
/// processor's dependencies live exactly as long as the processing does — one
/// DmDbContext per scope holds here the way it holds in a request.
/// </remarks>
public interface IProcessor<in TKey, in TMessage>
{
    /// <summary>Processes one delivered message.</summary>
    /// <param name="key">Routing key the message arrived under.</param>
    /// <param name="message">The decoded message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ProcessResult> Process(TKey key, TMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// What a consumer middleware may know about the delivery it wraps.
/// </summary>
/// <remarks>
/// Deliberately this small: the middlewares of this system measure and retry,
/// and neither needs the raw delivery. Anything added here becomes part of the
/// contract every pipeline shares.
/// </remarks>
/// <param name="routingKey">Routing key the message arrived under.</param>
/// <param name="redelivered">Whether the broker has delivered this message before.</param>
public sealed class ConsumerContext(string routingKey, bool redelivered)
{
    /// <summary>Routing key the message arrived under.</summary>
    public string RoutingKey { get; } = routingKey;

    /// <summary>Whether the broker has delivered this message before.</summary>
    public bool Redelivered { get; } = redelivered;
}

/// <summary>The rest of the consumer pipeline, processor included.</summary>
public delegate Task<ProcessResult> ConsumerDelegate(ConsumerContext context, CancellationToken cancellationToken);

/// <summary>
/// One step of a consumer pipeline, wrapped around the processor.
/// </summary>
public interface IConsumerMiddleware
{
    /// <summary>Runs the rest of the pipeline on its own terms.</summary>
    /// <param name="context">Consumer context.</param>
    /// <param name="next">Rest of the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ProcessResult> InvokeAsync(
        ConsumerContext context, ConsumerDelegate next, CancellationToken cancellationToken);
}
