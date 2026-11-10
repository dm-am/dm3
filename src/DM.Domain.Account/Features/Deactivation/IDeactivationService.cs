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
    /// The account screens offer nothing: a reader who wants his account gone writes
    /// to support. The reach is a ban's (ModerationRank), and a reason is required
    /// for the next moderator. The row stays, the address and the name stay reserved.
    /// </remarks>
    /// <param name="username">Whose account to remove</param>
    /// <param name="reason">Why, for the moderation record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeactivateByModeration(string username, string reason,
        CancellationToken cancellationToken = default);

    /// <remarks>
    /// Who may do it is the removal's rule; whose account is not limited by rank,
    /// because giving an account back is no act against its owner.
    /// </remarks>
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
    Task<(IReadOnlyList<RemovedAccountRow> Accounts, PagingResult Paging)> GetRemovedAccounts(
        RemovedAccountsQuery query, CancellationToken cancellationToken = default);
}
