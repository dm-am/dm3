using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// Service for username change requests
/// </summary>
public interface IUsernameChangeService
{
    /// <summary>
    /// Create a username change request
    /// </summary>
    Task<UsernameChangeRequestEntry> CreateAsync(CreateUsernameChangeRequest request);

    /// <summary>
    /// Get current user's latest request
    /// </summary>
    Task<UsernameChangeRequestEntry?> GetCurrentUserRequestAsync();

    /// <summary>
    /// Get all pending requests (admin)
    /// </summary>
    Task<IReadOnlyCollection<UsernameChangeRequestEntry>> GetPendingRequestsAsync();

    /// <summary>
    /// Get request by ID (admin)
    /// </summary>
    Task<UsernameChangeRequestEntry> GetByIdAsync(Guid requestId);

    /// <summary>
    /// Resolve (approve/reject) a request (admin)
    /// </summary>
    Task<UsernameChangeRequestEntry> ResolveAsync(ResolveUsernameChangeRequest resolve);

    /// <summary>
    /// What the page reached from the approval letter may do with the token it carries.
    /// </summary>
    /// <param name="token">Approval token from the letter</param>
    /// <returns>The state of the token, or null when no request was ever issued for it</returns>
    Task<UsernameChangeApprovalInfo?> GetApprovalInfoAsync(Guid token);

    /// <summary>
    /// Complete username change using approval token
    /// </summary>
    Task<UsernameChangeRequestEntry> CompleteWithTokenAsync(Guid token, string newUsername);

    /// <summary>
    /// Rollback a completed username change (moderator action)
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <returns>Updated request entry</returns>
    Task<UsernameChangeRequestEntry> RollbackAsync(Guid requestId);
}

/// <summary>
/// State of an approval token, as the page opened from the letter has to show it.
/// </summary>
/// <remarks>
/// Three answers rather than one, for the same reason as
/// <see cref="DM.Domain.Account.Features.PasswordChange.PasswordResetTokenInfo" />:
/// a link that is dead and a link that was already spent call for opposite things
/// from the reader — one is asked for a new request, the other for nothing at all —
/// and a single refusal makes the page guess. A token nobody was ever issued is
/// null, and is the fourth answer.
/// </remarks>
/// <param name="Status">One of "ready", "expired", "used"</param>
/// <param name="CurrentUsername">
/// The name being changed, for the ready state only: on a spent or dead link the
/// holder is told what happened and nothing about the account.
/// </param>
public record UsernameChangeApprovalInfo(string Status, string? CurrentUsername)
{
    /// <summary>The approval stands and the name can be chosen now.</summary>
    public static UsernameChangeApprovalInfo Ready(string currentUsername) => new("ready", currentUsername);

    /// <summary>The window for using the approval has closed.</summary>
    public static UsernameChangeApprovalInfo Expired() => new("expired", null);

    /// <summary>The name was already changed through this link.</summary>
    public static UsernameChangeApprovalInfo Used() => new("used", null);
}
