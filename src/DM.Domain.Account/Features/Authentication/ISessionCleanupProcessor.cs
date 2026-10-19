using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Drops sessions whose expiration has passed.
/// </summary>
/// <remarks>
/// A stored session is still a way in, so the sweep reads the domain's clock.
/// </remarks>
public interface ISessionCleanupProcessor
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <returns>What the pass removed.</returns>
    Task<SessionPurgeResult> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}
