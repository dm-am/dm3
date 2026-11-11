using System.Threading;
using System.Threading.Tasks;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// What became of one handled outbox row or push.
/// </summary>
/// <remarks>
/// No Retry member: a throw is the failure that spends an attempt of the row and retries it later.
/// </remarks>
public enum ProcessResult
{
    /// <summary>The row or the push is done with.</summary>
    Success,

    /// <summary>The row can never be processed, so it is dead at once.</summary>
    Failure,
}

/// <summary>
/// Handler of the rows of one queue, or of the pushes of the push channel.
/// </summary>
/// <remarks>
/// Resolved from a scope per row or push, so one DmDbContext per scope holds as in a request.
/// </remarks>
public interface IProcessor<in TKey, in TMessage>
{
    /// <summary>Processes one row or push.</summary>
    /// <param name="key">The queue or the push channel it arrived on.</param>
    /// <param name="message">What the row or the push holds, decoded.</param>
    Task<ProcessResult> Process(TKey key, TMessage message, CancellationToken cancellationToken);
}
