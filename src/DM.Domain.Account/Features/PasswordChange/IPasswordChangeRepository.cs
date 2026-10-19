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
    /// hash of it. Type, removal and age are checked inside.
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
    /// Save the new password and kill every live reset link of the account with it
    /// </summary>
    /// <remarks>
    /// Every live link, whichever door the change came in by, so an old letter cannot
    /// replace the new password. The same write drops a pending change of address and
    /// its link: a new password locks out whoever asked for that change.
    /// </remarks>
    /// <param name="userId">User identifier</param>
    /// <param name="passwordHash">New password hash</param>
    /// <param name="salt">New password salt</param>
    /// <param name="secretToInvalidate">
    /// The reset secret this change came in through, if it came in through one.
    /// Names the caller's route and no longer selects what is spent: the whole
    /// live set goes either way, and this secret is part of it.
    /// </param>
    Task UpdatePassword(Guid userId, string passwordHash, string salt, Guid? secretToInvalidate);
}
