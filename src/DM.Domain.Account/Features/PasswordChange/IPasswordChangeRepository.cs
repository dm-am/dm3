using System;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.PasswordChange;

/// <summary>
/// Storage for password change
/// </summary>
public interface IPasswordChangeRepository
{
    /// <summary>
    /// Find existing user by username
    /// </summary>
    Task<AuthenticatedUser?> FindUser(string username);

    /// <summary>
    /// Find the user a live reset secret belongs to
    /// </summary>
    /// <remarks>
    /// Takes the value from the letter, not a row identifier: the row keeps only a
    /// hash of it. Type, removal and age are checked inside — the invariant used to
    /// depend on a validator having run first.
    /// </remarks>
    /// <param name="secret">Value from the confirmation link</param>
    /// <param name="createdSince">Oldest moment a token may have been issued at</param>
    Task<AuthenticatedUser?> FindUser(Guid secret, DateTimeOffset createdSince);

    /// <summary>
    /// Whether a reset secret is live
    /// </summary>
    /// <param name="secret">Value from the confirmation link</param>
    /// <param name="createdSince">Oldest moment a token may have been issued at</param>
    Task<bool> TokenValid(Guid secret, DateTimeOffset createdSince);

    /// <summary>
    /// Save user password changes
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="passwordHash">New password hash</param>
    /// <param name="salt">New password salt</param>
    /// <param name="secretToInvalidate">Optional reset secret to spend</param>
    Task UpdatePassword(Guid userId, string passwordHash, string salt, Guid? secretToInvalidate);
}
