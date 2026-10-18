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
    /// Save the new password and kill every live reset link of the account with it
    /// </summary>
    /// <remarks>
    /// Every live link and not only the one the change came in through, whichever
    /// door it came in by. A link requested while a change was in flight outlived
    /// the change and set the password again afterwards, and a change made from
    /// inside a session left every earlier link alive by design - two ways into
    /// the same state, where a password the owner has just replaced can still be
    /// replaced by somebody holding an old letter.
    /// </remarks>
    /// <param name="userId">User identifier</param>
    /// <param name="passwordHash">New password hash</param>
    /// <param name="salt">New password salt</param>
    /// <param name="secretToInvalidate">
    /// The reset secret this change came in through, if it came in through one.
    /// Names the caller's route and no longer selects what is spent: the whole
    /// live set goes either way, and this secret is part of it.
    /// </param>
    /// <remarks>
    /// The same write drops a pending change of address and its confirmation
    /// link: a new password is the owner's way of locking somebody out, and an
    /// address change that person asked for must not complete from their
    /// mailbox afterwards.
    /// </remarks>
    Task UpdatePassword(Guid userId, string passwordHash, string salt, Guid? secretToInvalidate);
}
