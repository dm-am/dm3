using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
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
    /// Retire the account's live change links, record the requested address as
    /// pending and create the confirmation token, in one transaction
    /// </summary>
    /// <remarks>
    /// The account keeps answering at its current address until the link is
    /// followed. Anything else makes the letter a formality and a typo a locked
    /// account.
    ///
    /// Retiring and requesting are one write because they are one decision: as
    /// two, a second request from another tab interleaved with the first and left
    /// two live letters, the older of them pointing at an address the owner had
    /// already replaced.
    /// </remarks>
    Task RequestChange(Guid userId, string newEmail, CreateToken token);

    /// <summary>
    /// Move the pending address into the account. Null when nothing is pending
    /// </summary>
    /// <remarks>
    /// Throws <see cref="DuplicateEntityException" /> when the address stopped
    /// being free between the request and the click: it is the same refusal the
    /// form gives, arriving later because nothing but the unique index can hold
    /// the address in the meantime.
    /// </remarks>
    Task<EmailMove?> ApplyPendingEmail(Guid userId);
}

/// <summary>
/// The move that happened: the address the account had and the one it has now
/// </summary>
/// <remarks>
/// Read out of the same write that performed it. The previous address cannot be
/// looked up afterwards, and it is exactly the address the second letter goes to.
/// </remarks>
/// <param name="PreviousEmail">Address the account had</param>
/// <param name="NewEmail">Address it moved to</param>
public record EmailMove(string PreviousEmail, string NewEmail);
