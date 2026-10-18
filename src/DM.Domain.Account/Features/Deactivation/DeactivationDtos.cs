using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Account.Features.Deactivation;

/// <summary>
/// A removed account, as the only screen that can see one reads it
/// </summary>
/// <remarks>
/// Every row behind this type is invisible to the rest of the product: the
/// global filter over removed rows drops it from every list, every profile and
/// every lookup, which is exactly what removal means here. Moderation is the one
/// place that still has to see such a row - to bring it back - so this is the
/// one type that carries one.
/// </remarks>
/// <param name="UserId">Account identifier</param>
/// <param name="Username">Name the account keeps while it is gone</param>
/// <param name="Email">
/// Address the account keeps, where its letters go. Filled by the storage and
/// left for an administrator only: a moderator reads a removed profile without
/// the address everywhere else, and a list must not be the exception.
/// </param>
/// <param name="Role">Role the account had</param>
/// <param name="RegisteredUtc">When the account was registered</param>
/// <param name="LastActivityUtc">When it was last seen, if it ever was</param>
public record RemovedAccount(
    Guid UserId,
    string Username,
    string? Email,
    UserRole Role,
    DateTimeOffset RegisteredUtc,
    DateTimeOffset? LastActivityUtc);

/// <summary>
/// Which removed accounts to list
/// </summary>
/// <remarks>
/// A moderator answering a support letter has a name or an address and nothing
/// else, and the removed are the one set he cannot reach through the usual user
/// search. Both are matched by the same term, because whoever wrote the letter
/// decides which of the two he quoted.
/// </remarks>
public class RemovedAccountsQuery : PagingQuery
{
    /// <summary>
    /// Part of a name or of an address; empty lists every removed account
    /// </summary>
    public string? Search { get; set; }
}
