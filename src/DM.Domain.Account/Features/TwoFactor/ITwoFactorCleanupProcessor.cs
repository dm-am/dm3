using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// The pass that clears away setups nobody finished.
/// </summary>
/// <remarks>
/// Not retention over a stream: a secret issued and never confirmed is an
/// abandoned credential, and it has to go for the same reason an unfinished
/// registration does. Without the pass the table quietly fills with secrets
/// belonging to people who have forgotten they asked for them.
/// </remarks>
public interface ITwoFactorCleanupProcessor
{
    /// <summary>
    /// Delete every unconfirmed setup older than the setup window.
    /// </summary>
    /// <returns>How many were deleted</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> DeleteAbandonedAsync(CancellationToken cancellationToken = default);
}
