using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Drops sessions whose expiration has passed.
/// </summary>
/// <remarks>
/// A session is authorization: while its document says it is there, a stolen
/// token is still a way in. Which is why the moment the sweep compares against
/// comes from the clock the domain is given, and why the sweep is callable from
/// the domain rather than only from a running host.
/// </remarks>
public interface ISessionCleanupProcessor
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What the pass removed.</returns>
    Task<SessionPurgeResult> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}
