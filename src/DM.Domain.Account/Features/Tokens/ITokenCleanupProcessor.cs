using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Tokens;

/// <summary>
/// Drops tokens the account area no longer keeps.
/// </summary>
/// <remarks>
/// The retention window is a product decision and lives with the rest of them in
/// <see cref="DM.Domain.Account.Configuration.AccountRetentionPolicy" />; what a
/// caller supplies is the moment the pass runs at, which comes from the clock the
/// domain is given rather than from the machine one.
/// </remarks>
public interface ITokenCleanupProcessor
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of tokens deleted.</returns>
    Task<int> DeleteStaleAsync(CancellationToken cancellationToken = default);
}
