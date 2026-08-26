using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Core.Retention;

/// <summary>
/// One pass of the retention sweep: deletes every row whose retention term has
/// run out, across every table that declares one.
/// </summary>
/// <remarks>
/// Which tables carry a term and how long each term is belongs to the
/// persistence layer's retention registry; this contract only lets the host ask
/// for a pass and log what it removed.
/// </remarks>
public interface IRetentionSweepProcessor
{
    /// <summary>
    /// Run one sweep over every declared retention policy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What the pass removed, per table.</returns>
    Task<IReadOnlyList<RetentionSweepResult>> SweepAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// What one sweep removed from one table.
/// </summary>
/// <param name="Table">Table the policy belongs to.</param>
/// <param name="RowsDeleted">Rows whose term had run out.</param>
public record RetentionSweepResult(string Table, int RowsDeleted);
