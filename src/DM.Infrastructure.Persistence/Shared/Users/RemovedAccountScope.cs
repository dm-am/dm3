using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Users;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Shared.Users;

/// <summary>
/// The rows behind "Аккаунт удален": every removed account, as a set of identifiers.
/// </summary>
/// <remarks>
/// The one read of the users table past the account filter that the profile of
/// removed accounts and the lists of its tabs make (<see cref="RemovedAccount" />). It
/// runs as a statement of its own, never as a subquery: lifting a filter is a flag on
/// the whole query, so an inline subquery would lift the filters of everything around
/// it too, and a list of games asked for by host would start answering with removed
/// games. The identifiers stay on the server: they narrow a list the caller then reads
/// under his own access rules, and nothing that answers him carries one.
/// </remarks>
internal static class RemovedAccountScope
{
    /// <summary>Every removed account.</summary>
    public static async Task<IReadOnlyCollection<Guid>> Ids(DbSet<User> users, CancellationToken ct = default) =>
        await users
            .IgnoreQueryFilters()
            .TagWith("DM.User.RemovedAccountIds")
            .Where(u => u.IsRemoved)
            .Select(u => u.UserId)
            .ToListAsync(ct);

    /// <summary>
    /// The authors a list asked by name means, when the names include "Аккаунт
    /// удален": every removed account, and the live accounts under the other names
    /// asked alongside it. Null when the name is not among them - the caller keeps
    /// the filter it had.
    /// </summary>
    /// <remarks>
    /// Both halves as identifiers, so the list narrows by the author column alone and
    /// joins no account: the count and the page of a list are then one question
    /// whether the page lifts the account filter or not. The other names are read
    /// under the filter, as every name is - a removed login is nobody's.
    /// </remarks>
    public static async Task<IReadOnlyCollection<Guid>?> IdsIfAsked(
        DbSet<User> users, IEnumerable<string>? names, CancellationToken ct = default)
    {
        var asked = names?.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray() ?? [];
        if (!RemovedAccount.IsAmong(asked))
        {
            return null;
        }

        var removed = await Ids(users, ct);
        var others = asked
            .Where(name => !RemovedAccount.Is(name))
            .Select(name => name.Trim().ToLower())
            .ToArray();
        if (others.Length == 0)
        {
            return removed;
        }

        var named = await users
            .TagWith("DM.User.NamedAlongsideRemoved")
            .Where(u => others.Contains(u.Username.ToLower()))
            .Select(u => u.UserId)
            .ToListAsync(ct);
        return removed.Concat(named).ToList();
    }

    /// <summary>
    /// The public ratings of every removed account, added up: the posts they wrote
    /// and the score those posts earned.
    /// </summary>
    public static async Task<(int Quality, int Quantity)> RatingTotals(
        DbSet<User> users, CancellationToken ct = default)
    {
        var totals = await users
            .IgnoreQueryFilters()
            .TagWith("DM.User.RemovedAccountRatings")
            .Where(u => u.IsRemoved)
            .GroupBy(_ => 1)
            .Select(g => new { Quality = g.Sum(u => u.QualityRating), Quantity = g.Sum(u => u.QuantityRating) })
            .FirstOrDefaultAsync(ct);
        return totals is null ? (0, 0) : (totals.Quality, totals.Quantity);
    }
}
