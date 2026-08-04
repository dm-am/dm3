using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// Closes name change requests that ran out of time.
/// </summary>
/// <remarks>
/// Two independent deadlines, kept as two calls: one is the moderators' review
/// window, the other is the requester's window to use an approval they were
/// given. A caller that fails on one still has to run the other, which a single
/// method would not let it do.
/// </remarks>
public interface IUsernameChangeExpiryProcessor
{
    /// <summary>
    /// Expires requests no moderator looked at within the review window.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of requests expired.</returns>
    Task<int> ExpireUnreviewedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires approvals the requester never used before their token ran out.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of requests expired.</returns>
    Task<int> ExpireApprovalTokensAsync(CancellationToken cancellationToken = default);
}
