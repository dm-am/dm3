using System.Threading;
using System.Threading.Tasks;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// What became of one handled message.
/// </summary>
/// <remarks>
/// No Retry member: a throw is the failure that spends an attempt of the row and retries it later.
/// </remarks>
public enum ProcessResult
{
    /// <summary>The message is done with.</summary>
    Success,

    /// <summary>The message can never be processed, so its row is dead at once.</summary>
    Failure,
}

/// <summary>
/// Handler of the messages of one queue.
/// </summary>
/// <remarks>
/// Resolved from a scope per message, so one DmDbContext per scope holds as in a request.
/// </remarks>
public interface IProcessor<in TKey, in TMessage>
{
    /// <summary>Processes one message.</summary>
    /// <param name="key">The queue or the push channel the message arrived on.</param>
    /// <param name="message">The decoded message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ProcessResult> Process(TKey key, TMessage message, CancellationToken cancellationToken);
}
