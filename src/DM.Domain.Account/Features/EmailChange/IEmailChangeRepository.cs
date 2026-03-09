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
    /// Update user email and create confirmation token
    /// </summary>
    Task Update(Guid userId, string newEmail, CreateToken token);

    /// <summary>
    /// Invalidate old email change tokens for a user
    /// </summary>
    Task InvalidateOldEmailChangeTokens(Guid userId);
}
