using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Account.Features.Deactivation;

/// <summary>
/// Service for account deactivation (soft delete)
/// </summary>
/// <remarks>
/// Removal and restoration are a moderator's, they happen the moment he asks for
/// them, and there is no queue in front of either: a reader who wants his account
/// gone or back writes to support like he would about anything else, and a person
/// reads that letter and acts here.
/// </remarks>
public interface IDeactivationService
{
    /// <summary>
    /// Deactivate somebody else's account on a moderator's decision.
    /// </summary>
    /// <remarks>
    /// The door the owner asked for and asked not to advertise: the account
    /// screens offer nothing, and a reader who wants his account gone writes to
    /// support. A senior moderator then does it here, with the same reach a ban
    /// has - never over an administrator and never over his own rank - and with a
    /// reason, because an account removed without one leaves the next moderator
    /// nothing to read.
    ///
    /// The deletion itself is the one the owner already has: the row stays, the
    /// address and the name stay reserved, and the content stays where it is
    /// without its author's name. The letter is what tells its owner, at the
    /// address the account keeps.
    /// </remarks>
    /// <param name="username">Whose account to remove</param>
    /// <param name="reason">Why, for the moderation record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeactivateByModeration(string username, string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bring a removed account back on a moderator's decision.
    /// </summary>
    /// <remarks>
    /// The mirror of the removal, and the whole of what restoration means here:
    /// no request to file, nothing to wait for, and a letter telling the owner
    /// his account answers again.
    ///
    /// Who may do it is the removal's rule - a senior moderator or an
    /// administrator. Whose account it may be done to is not: the removal refuses
    /// an equal rank because taking an account from an equal is an act against
    /// him, while giving one back is not, and the same refusal here would leave a
    /// moderator's own removed account with nobody able to restore it.
    /// </remarks>
    /// <param name="username">Whose account to bring back</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RestoreByModeration(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// The removed accounts, which no other list can show.
    /// </summary>
    /// <remarks>
    /// A removed row is hidden from every other read in the product, so without
    /// this a moderator holding a support letter has no way to find the account
    /// it is about - or to see that it was never removed in the first place.
    /// </remarks>
    /// <param name="query">Search term and page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The page and what it is a page of</returns>
    Task<(IReadOnlyList<RemovedAccount> Accounts, PagingResult Paging)> GetRemovedAccounts(
        RemovedAccountsQuery query, CancellationToken cancellationToken = default);
}
