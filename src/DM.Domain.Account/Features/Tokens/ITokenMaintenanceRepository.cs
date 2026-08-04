using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Tokens;

/// <summary>
/// Storage side of token retention.
/// </summary>
public interface ITokenMaintenanceRepository
{
    /// <summary>
    /// Deletes tokens that were withdrawn, and tokens issued before
    /// <paramref name="issuedBefore" /> whatever their state.
    /// </summary>
    /// <param name="issuedBefore">Cutoff the caller derived from the retention policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows removed.</returns>
    Task<int> DeleteWithdrawnOrIssuedBefore(DateTimeOffset issuedBefore, CancellationToken cancellationToken = default);
}
