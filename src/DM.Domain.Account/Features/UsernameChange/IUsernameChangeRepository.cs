using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// Repository for username change request operations
/// </summary>
public interface IUsernameChangeRepository
{
    /// <summary>
    /// Get pending request for user
    /// </summary>
    Task<UsernameChangeRequest?> GetPendingByUserId(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get latest request for user (any status)
    /// </summary>
    Task<UsernameChangeRequest?> GetLatestByUserId(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get request by ID
    /// </summary>
    Task<UsernameChangeRequest?> GetById(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Get all pending requests
    /// </summary>
    Task<IReadOnlyCollection<UsernameChangeRequestEntry>> GetPendingRequests(CancellationToken ct = default);

    /// <summary>
    /// Get request by approval token
    /// </summary>
    Task<UsernameChangeRequest?> GetByApprovalToken(Guid token, CancellationToken ct = default);

    /// <summary>
    /// Create a new request
    /// </summary>
    Task Add(UsernameChangeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update an existing request
    /// </summary>
    Task Update(UsernameChangeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Writes the history row, the new username and the resolved request in one commit
    /// </summary>
    /// <remarks>
    /// Three writes rather than one left states between them that the model has no
    /// name for: a history row against a user still carrying the old name and, worst
    /// of the three, a renamed user whose request is still pending - which is one
    /// approval spent on two renames.
    /// </remarks>
    Task ApplyRename(UsernameChangeRequest request, CreateUsernameHistory history, CancellationToken ct = default);

    /// <summary>
    /// Check if username is available (not taken by another user)
    /// </summary>
    Task<bool> IsUsernameAvailable(string username, Guid? excludeUserId = null, CancellationToken ct = default);


    /// <summary>
    /// Expire requests still waiting for a moderator since before
    /// <paramref name="createdBefore" />.
    /// </summary>
    /// <param name="createdBefore">Cutoff the caller derived from the review window.</param>
    /// <param name="resolvedUtc">Moment written as the resolution time.</param>
    /// <param name="comment">Resolution comment the requester is shown.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of requests expired.</returns>
    Task<int> ExpireUnreviewedRequests(
        DateTimeOffset createdBefore, DateTimeOffset resolvedUtc, string comment, CancellationToken ct = default);

    /// <summary>
    /// Expire approved requests whose approval token has run out by
    /// <paramref name="now" />, and withdraw the token.
    /// </summary>
    /// <param name="now">Moment the pass runs at; the stored expiration is compared against it.</param>
    /// <param name="commentSuffix">Text appended to the resolution comment already there.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of requests expired.</returns>
    Task<int> ExpireApprovalTokens(DateTimeOffset now, string commentSuffix, CancellationToken ct = default);
}
