using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Runs a write that must survive being replayed.
/// </summary>
/// <remarks>
/// Through the execution strategy because the API host configures
/// EnableRetryOnFailure, and a retrying strategy refuses a transaction opened by
/// hand: the block has to be handed to the strategy so the strategy owns the
/// retry, transaction and all.
///
/// The change tracker is cleared before a replay. A retry runs the whole block
/// again, and SaveChanges leaves an entity Unchanged even when the transaction
/// around it rolled back - so without the clear the second attempt either writes
/// the row twice (still Added) or writes nothing at all (already Unchanged), and
/// in the soft-delete cases it takes the side effects while writing no deletion.
/// That is the general rule; what exactly would have stayed tracked is a fact
/// about each caller and stays at the call site.
///
/// Nineteen writes carried this preamble word for word before it lived here.
/// </remarks>
internal static class RetryableWrite
{
    /// <summary>
    /// Run <paramref name="write" /> through the strategy.
    /// </summary>
    public static Task Run(DmDbContext dbContext, Func<Task> write) =>
        Run(dbContext, (_, _) => write());

    /// <summary>
    /// Run <paramref name="write" /> through the strategy, telling it whether
    /// this is a replay. For the caller that has to re-read a row the clear just
    /// detached before it can carry on.
    /// </summary>
    public static Task Run(DmDbContext dbContext, Func<bool, Task> write) =>
        Run(dbContext, (isRetry, _) => write(isRetry));

    /// <summary>
    /// Run <paramref name="write" /> through the strategy, on the token the
    /// strategy hands it.
    /// </summary>
    public static Task Run(
        DmDbContext dbContext, Func<CancellationToken, Task> write, CancellationToken ct) =>
        Run(dbContext, (_, cancellation) => write(cancellation), ct);

    private static Task Run(
        DmDbContext dbContext,
        Func<bool, CancellationToken, Task> write,
        CancellationToken ct = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var attempted = false;
        return strategy.ExecuteAsync(async cancellation =>
        {
            var isRetry = attempted;
            if (isRetry)
            {
                dbContext.ChangeTracker.Clear();
            }

            attempted = true;
            await write(isRetry, cancellation);
        }, ct);
    }
}
