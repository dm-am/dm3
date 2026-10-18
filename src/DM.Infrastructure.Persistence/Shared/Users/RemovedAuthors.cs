using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Shared.Users;

/// <summary>
/// What a removed account wrote is read with the account row in reach, and signed
/// by nobody.
/// </summary>
/// <remarks>
/// The account row is hidden by the soft-delete filter, like every removable row.
/// The author of a topic, a post, a comment, a publication, a message or a review
/// and the master of a game are required navigations, and EF joins a required
/// navigation to a filtered row with an INNER JOIN: the row the removed account
/// wrote fell out of the read together with its author. A game whose master left
/// vanished from the catalogue, a post from the room it moved on, and after the
/// DM2 import every account removed there over twenty years would have taken its
/// share of every old game with it.
///
/// So the filter over Users is a named one, and the content reads lift exactly
/// that one, through <see cref="KeepingRemovedAuthors{T}" /> in their projection
/// entry points. Every other filter of the query stays: a removed topic, post or
/// room is still invisible. With the account filter lifted the join keeps the row
/// (the foreign key guarantees the account exists), and the nested user formula,
/// <see cref="GeneralUserProjections.Projection" />, answers null for a removed
/// account - the client signs a missing author as "Аккаунт удален". Nothing of the
/// removed row reaches the reader: not the name, not the address, not the avatar.
///
/// The price is that a lifted query sees removed accounts wherever it reaches a
/// user, and so everything else in it that reads a user has to say what it does
/// with a removed one: a roster member is left out, a filter by name does not
/// match a removed name. Lifting the filter for the whole model instead would have
/// put that burden on every read of a user in the product, the sign-in and the
/// user lists included, where one forgotten predicate brings a removed account
/// back; lifted here, a forgotten read keeps the old behaviour and loses a row.
/// The alternative that keeps the filter everywhere, a correlated read of the
/// author, is translated by EF as ROW_NUMBER over the whole users table for every
/// page of every list.
/// </remarks>
internal static class RemovedAuthors
{
    /// <summary>
    /// The key of the soft-delete filter over Users. Every other removable entity
    /// keeps the anonymous filter, and lifting this key leaves those in force.
    /// </summary>
    public const string AccountFilter = "RemovedAccounts";

    /// <summary>
    /// The query keeps the rows removed accounts wrote; nested users are then
    /// answered by the formulas that return null for a removed account.
    /// </summary>
    public static IQueryable<T> KeepingRemovedAuthors<T>(this IQueryable<T> query)
        where T : class =>
        query.IgnoreQueryFilters([AccountFilter]);
}
