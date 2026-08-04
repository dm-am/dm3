using System;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <summary>
/// The numbers the user list is sorted and filtered by, and the join that brings
/// them to the page.
/// </summary>
/// <remarks>
/// None of the four — games hosted, games played, blogs hosted, active subscribers —
/// is a column of Users, and each used to be spelled as a <c>Count()</c> correlated
/// to the user row inside ORDER BY or WHERE. That form runs the aggregate once per row
/// of Users and runs it before OFFSET, so a page of fifty cost a pass over every
/// registration and asking for fewer rows changed nothing.
///
/// Here each counter is one GROUP BY over its own table, left-joined to the page once.
/// A user the counter has no row for counts zero, which is the number the correlated
/// form returned for him.
///
/// Written as a separate class rather than as members of the repository because the
/// statement it produces is the thing under test: <c>ToQueryString</c> on these
/// queries is what states that no subquery correlates to the outer row, and reaching
/// it through the repository would need a Mongo client and a mapper to say nothing
/// extra.
/// </remarks>
internal static class UserCountQueries
{
    /// <summary>
    /// A number the user list can be ordered by and filtered by. Named as one thing
    /// because it is one: the sort and the numeric range over the same counter must
    /// reach the same join, or the statement counts the table twice for one page.
    /// </summary>
    internal enum Counter
    {
        /// <summary>Games mastered plus games assisted.</summary>
        GamesHosting,

        /// <summary>Blogs owned plus blogs assisted.</summary>
        BlogsHosting,

        /// <summary>Distinct games the user has a character in.</summary>
        GamesPlaying,

        /// <summary>Subscribers active within the activity window.</summary>
        Popularity,
    }

    /// <summary>
    /// One row per user with the number of rows counted for him in one table.
    /// </summary>
    internal sealed class UserCount
    {
        /// <summary>The user counted for.</summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// His rows in that table. Nullable because the join below is a left one and
        /// the counter has no row for a user with nothing to count: the member reads
        /// as NULL for him, and the caller coalesces it to the zero the correlated
        /// <c>Count()</c> used to return. A non-nullable member would make the check
        /// a null test on the projected object, which is not translatable.
        /// </summary>
        public int? Count { get; set; }
    }

    /// <summary>
    /// A user row carrying the number the list is ordered or filtered by.
    /// </summary>
    internal sealed class CountedUser
    {
        /// <summary>The user.</summary>
        public User User { get; set; } = null!;

        /// <summary>His counter.</summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Games the user runs as the master.
    /// </summary>
    internal static IQueryable<UserCount> GamesMastered(DmDbContext context) =>
        context.Games
            .Where(g => !g.IsRemoved)
            .GroupBy(g => g.MasterId)
            .Select(g => new UserCount { UserId = g.Key, Count = g.Count() });

    /// <summary>
    /// Games the user runs as an assistant.
    /// </summary>
    internal static IQueryable<UserCount> GamesAssisted(DmDbContext context) =>
        context.GameAssistants
            .Where(a => !a.Game.IsRemoved)
            .GroupBy(a => a.UserId)
            .Select(g => new UserCount { UserId = g.Key, Count = g.Count() });

    /// <summary>
    /// Blogs the user owns.
    /// </summary>
    internal static IQueryable<UserCount> BlogsOwned(DmDbContext context) =>
        context.Blogs
            .Where(b => !b.IsRemoved)
            .GroupBy(b => b.AuthorId)
            .Select(g => new UserCount { UserId = g.Key, Count = g.Count() });

    /// <summary>
    /// Blogs the user runs as an assistant.
    /// </summary>
    internal static IQueryable<UserCount> BlogsAssisted(DmDbContext context) =>
        context.BlogAssistants
            .Where(a => !a.Blog.IsRemoved)
            .GroupBy(a => a.UserId)
            .Select(g => new UserCount { UserId = g.Key, Count = g.Count() });

    /// <summary>
    /// Games the user plays in: distinct games, because two characters in one game
    /// are one game played.
    /// </summary>
    internal static IQueryable<UserCount> GamesPlayed(DmDbContext context) =>
        context.Characters
            .Where(c => !c.IsRemoved && !c.IsNpc && c.AuthorId.HasValue && !c.Game.IsRemoved)
            .Select(c => new { UserId = c.AuthorId!.Value, c.GameId })
            .Distinct()
            .GroupBy(x => x.UserId)
            .Select(g => new UserCount { UserId = g.Key, Count = g.Count() });

    /// <summary>
    /// Subscribers of the user who have been active since the given moment, which
    /// is what the popularity sort means by popular.
    /// </summary>
    internal static IQueryable<UserCount> ActiveSubscribers(DmDbContext context, DateTimeOffset activeSince) =>
        context.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.User &&
                s.Subscriber.LastActivityUtc.HasValue && s.Subscriber.LastActivityUtc.Value > activeSince)
            .GroupBy(s => s.TargetId)
            .Select(g => new UserCount { UserId = g.Key, Count = g.Count() });

    /// <summary>
    /// Left-join the users to one counter.
    /// </summary>
    internal static IQueryable<CountedUser> WithCount(IQueryable<User> users, IQueryable<UserCount> counts) =>
        from u in users
        join c in counts on u.UserId equals c.UserId into matched
        from c in matched.DefaultIfEmpty()
        select new CountedUser { User = u, Count = c.Count ?? 0 };

    /// <summary>
    /// The same for a counter that is the sum of two: hosting is owning plus
    /// assisting, and the two live in different tables.
    /// </summary>
    internal static IQueryable<CountedUser> WithCountSum(
        IQueryable<User> users, IQueryable<UserCount> first, IQueryable<UserCount> second) =>
        from u in users
        join f in first on u.UserId equals f.UserId into matchedFirst
        from f in matchedFirst.DefaultIfEmpty()
        join s in second on u.UserId equals s.UserId into matchedSecond
        from s in matchedSecond.DefaultIfEmpty()
        select new CountedUser { User = u, Count = (f.Count ?? 0) + (s.Count ?? 0) };

    /// <summary>
    /// The users left-joined to the counter they asked for.
    /// </summary>
    /// <remarks>
    /// One entry point for all four so a caller that needs the same number twice —
    /// once to select the page and once to order it — asks for it once. Building the
    /// filter's join in one place and the sort's join in another put two GROUP BYs
    /// over the same table into a statement that needs one.
    /// </remarks>
    internal static IQueryable<CountedUser> Counted(
        DmDbContext context, Counter counter, IQueryable<User> users, DateTimeOffset activeSince) =>
        counter switch
        {
            Counter.GamesHosting => WithCountSum(users, GamesMastered(context), GamesAssisted(context)),
            Counter.BlogsHosting => WithCountSum(users, BlogsOwned(context), BlogsAssisted(context)),
            Counter.GamesPlaying => WithCount(users, GamesPlayed(context)),
            Counter.Popularity => WithCount(users, ActiveSubscribers(context, activeSince)),
            _ => throw new ArgumentOutOfRangeException(nameof(counter), counter, "unknown user counter"),
        };

    /// <summary>
    /// Keep the users whose counter falls inside the range, still carrying it.
    /// </summary>
    internal static IQueryable<CountedUser> InRangeCounted(
        IQueryable<CountedUser> counted, int? min, int? max)
    {
        if (min.HasValue)
        {
            counted = counted.Where(x => x.Count >= min.Value);
        }

        if (max.HasValue)
        {
            counted = counted.Where(x => x.Count <= max.Value);
        }

        return counted;
    }

    /// <summary>
    /// The same, handing back the user rows so a caller that does not need the number
    /// afterwards goes on composing over Users as it did.
    /// </summary>
    internal static IQueryable<User> InRange(IQueryable<CountedUser> counted, int? min, int? max) =>
        InRangeCounted(counted, min, max).Select(x => x.User);

    /// <summary>
    /// Order the page by the counter, ties broken by username.
    /// </summary>
    /// <remarks>
    /// The secondary key is ascending in both directions, as it was when the count
    /// was a subquery in the ORDER BY: it names the row, it is not a second opinion
    /// on the number.
    /// </remarks>
    internal static IOrderedQueryable<CountedUser> Order(IQueryable<CountedUser> counted, bool ascending) =>
        ascending
            ? counted.OrderBy(x => x.Count).ThenBy(x => x.User.Username)
            : counted.OrderByDescending(x => x.Count).ThenBy(x => x.User.Username);
}
