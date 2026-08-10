using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Tokens;

namespace DM.Domain.Account.Features.EmailChange;

/// <summary>
/// Storage for email changing
/// </summary>
public interface IEmailChangeRepository
{
    /// <summary>
    /// Find user by username
    /// </summary>
    Task<AuthenticatedUser?> FindUser(string username);

    /// <summary>
    /// Check if email is not taken by another user
    /// </summary>
    Task<bool> IsEmailFree(string email, CancellationToken ct);

    /// <summary>
    /// Record the requested address as pending and create the confirmation token
    /// </summary>
    /// <remarks>
    /// The account keeps answering at its current address until the link is
    /// followed. Anything else makes the letter a formality and a typo a locked
    /// account.
    /// </remarks>
    Task RequestChange(Guid userId, string newEmail, CreateToken token);

    /// <summary>
    /// Move the pending address into the account. False when nothing is pending
    /// </summary>
    Task<bool> ApplyPendingEmail(Guid userId);

    /// <summary>
    /// Invalidate old email change tokens for a user
    /// </summary>
    Task InvalidateOldEmailChangeTokens(Guid userId);
}
