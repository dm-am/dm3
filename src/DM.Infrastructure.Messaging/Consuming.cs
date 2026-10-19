using System.Threading;
using System.Threading.Tasks;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// What became of one delivered message.
/// </summary>
/// <remarks>
/// No Retry member: <see cref="RetryingConsumerMiddleware"/> retries with a ceiling, a broker requeue has none.
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
/// Resolved from a scope per message, so one DmDbContext per scope holds as in a request.
/// </remarks>
public interface IProcessor<in TKey, in TMessage>
{
    /// <summary>Processes one delivered message.</summary>
    /// <param name="key">Routing key the message arrived under.</param>
    /// <param name="message">The decoded message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ProcessResult> Process(TKey key, TMessage message, CancellationToken cancellationToken);
}

/// <summary>The rest of the consumer pipeline, processor included.</summary>
public delegate Task<ProcessResult> ConsumerDelegate(CancellationToken cancellationToken);

/// <summary>
/// One step of a consumer pipeline, wrapped around the processor.
/// </summary>
public interface IConsumerMiddleware
{
    /// <summary>Runs the rest of the pipeline on its own terms.</summary>
    /// <param name="next">Rest of the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ProcessResult> InvokeAsync(ConsumerDelegate next, CancellationToken cancellationToken);
}
