using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Core.Users;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

internal partial class UserRepository
{
    /// <inheritdoc />
    public Task<int> CountUsersAsync(UserFilter filter) => GetQuery(filter).CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsersAsync(PagingData paging, UserFilter filter)
    {
        var users = await BuildPageQuery(paging, filter)
            .ProjectToGeneralUser()
            .ToArrayAsync();

        await PopulateListCounts(users);
        return users;
    }

    /// <summary>
    /// The filtered, ordered and paged rows the user list draws, before projection.
    /// </summary>
    /// <remarks>
    /// Separated from the projection because the statement it composes is the thing
    /// under test — how many times a page counts a table is readable from
    /// <c>ToQueryString</c> and from nothing else.
    /// </remarks>
    internal IQueryable<User> BuildPageQuery(PagingData paging, UserFilter filter)
    {
        // A local, because nullable-flow analysis narrows a local, not a property.
        var search = filter.Search;

        // The four sorts over a derived number; null for every other sort, which orders
        // by a column of Users and needs no join at all.
        var sortCounter = CounterOf(filter.Sort);

        // No Include: the query ends in a projection, which makes EF drop every Include.
        //
        // The range over the counter this page is sorted by is left out here and applied
        // to the join below: filtering and ordering by the same number through two joins
        // put two GROUP BYs over the same table into one statement, and "min games
        // hosting, sorted by games hosting" is the ordinary way that filter is used.
        var baseQuery = GetQuery(filter, sortCounter);

        // When searching without explicit sort, use relevance-based ordering
        // When user explicitly selects a sort (e.g., rating), respect their choice
        if (!string.IsNullOrWhiteSpace(search) && filter.Sort == UserSort.Name)
        {
            return OrderByRelevance(baseQuery, search, filter.SortAscending).Page(paging);
        }

        if (sortCounter.HasValue)
        {
            return OrderByCounter(baseQuery, filter, sortCounter.Value)
                .Page(paging)
                .Select(x => x.User);
        }

        return OrderByColumn(baseQuery, filter.Sort, filter.SortAscending).Page(paging);
    }

    /// <summary>
    /// Relevance ranking: exact match, then prefix match, then similarity score
    /// (for fuzzy matches), then alphabetically within the same relevance.
    /// </summary>
    /// <remarks>
    /// The direction reaches the last key and no further. Relevance is what
    /// a search is for, so it leads whatever the caller asked; but the tie
    /// among equally relevant names is theirs to order.
    /// </remarks>
    private static IOrderedQueryable<User> OrderByRelevance(
        IQueryable<User> baseQuery,
        string search,
        bool sortAscending)
    {
        var searchLower = search.ToLower();
        var prefixPattern = LikePatterns.StartsWith(search);
        var relevance = baseQuery
            .OrderByDescending(u => u.Username.ToLower() == searchLower)
            .ThenByDescending(u => EF.Functions.ILike(u.Username, prefixPattern))
            .ThenByDescending(u => EF.Functions.TrigramsSimilarity(u.Username, searchLower));

        return sortAscending
            ? relevance.ThenBy(u => u.Username).ThenBy(u => u.UserId)
            : relevance.ThenByDescending(u => u.Username).ThenByDescending(u => u.UserId);
    }

    /// <summary>
    /// The page left-joined to one GROUP BY per counted table, so the aggregate runs
    /// once per request instead of once per row of Users.
    /// </summary>
    private IOrderedQueryable<UserCountQueries.CountedUser> OrderByCounter(
        IQueryable<User> baseQuery,
        UserFilter filter,
        UserCountQueries.Counter sortCounter)
    {
        var counted = UserCountQueries.Counted(_dbContext, sortCounter, baseQuery,
            _dateTimeProvider.Now - ActivityPolicy.ActivePeriod);
        var (min, max) = RangeOf(filter, sortCounter);
        counted = UserCountQueries.InRangeCounted(counted, min, max);
        return UserCountQueries.Order(counted, filter.SortAscending);
    }

    private IOrderedQueryable<User> OrderByColumn(IQueryable<User> baseQuery, UserSort sort, bool sortAscending)
    {
        var orderedQuery = sort switch
        {
            UserSort.Rating => sortAscending
                ? baseQuery.OrderBy(u => u.RatingDisabled).ThenBy(u => u.QualityRating).ThenBy(u => u.QuantityRating)
                : baseQuery.OrderBy(u => u.RatingDisabled).ThenByDescending(u => u.QualityRating).ThenByDescending(u => u.QuantityRating),
            // Online users first, then by activity time
            UserSort.LastActivity => sortAscending
                ? baseQuery.OrderBy(u => u.LastActivityUtc.HasValue && u.LastActivityUtc.Value > _dateTimeProvider.Now - ActivityPolicy.OnlinePeriod)
                    .ThenBy(u => u.LastActivityUtc)
                : baseQuery.OrderByDescending(u => u.LastActivityUtc.HasValue && u.LastActivityUtc.Value > _dateTimeProvider.Now - ActivityPolicy.OnlinePeriod)
                    .ThenByDescending(u => u.LastActivityUtc),
            UserSort.Registered => sortAscending
                ? baseQuery.OrderBy(u => u.CreatedUtc)
                : baseQuery.OrderByDescending(u => u.CreatedUtc),
            _ => sortAscending
                ? baseQuery.OrderBy(u => u.Username)
                : baseQuery.OrderByDescending(u => u.Username)
        };

        // A last key nothing can tie on. Every ordering above is over a value
        // users share — a rating, a registration date, a name — and a page is a
        // window over it: with the tie left to the database, two pages of one
        // list can repeat a row and drop another, and nothing in the answer says
        // so. The identifier is unique by construction and settles it.
        return sortAscending
            ? orderedQuery.ThenBy(u => u.UserId)
            : orderedQuery.ThenByDescending(u => u.UserId);
    }

    private static UserCountQueries.Counter? CounterOf(UserSort sort) => sort switch
    {
        UserSort.GamesHosting => UserCountQueries.Counter.GamesHosting,
        UserSort.BlogsHosting => UserCountQueries.Counter.BlogsHosting,
        UserSort.GamesPlaying => UserCountQueries.Counter.GamesPlaying,
        UserSort.Popularity => UserCountQueries.Counter.Popularity,
        _ => null,
    };

    /// <summary>
    /// The numeric range the filter states over one counter. Popularity has none:
    /// the list sorts by it and does not bound it.
    /// </summary>
    private static (int? Min, int? Max) RangeOf(UserFilter filter, UserCountQueries.Counter counter) =>
        counter switch
        {
            UserCountQueries.Counter.GamesHosting => (filter.MinGamesHosting, filter.MaxGamesHosting),
            UserCountQueries.Counter.GamesPlaying => (filter.MinGamesPlaying, filter.MaxGamesPlaying),
            UserCountQueries.Counter.BlogsHosting => (filter.MinBlogsHosting, filter.MaxBlogsHosting),
            _ => (null, null),
        };

    /// <param name="countedElsewhere">
    /// A counter whose numeric range the caller applies itself, because it also orders
    /// by that counter and the two share one join. Null — the count and every sort over
    /// a column of Users — applies all three ranges here.
    /// </param>
    private IQueryable<User> GetQuery(UserFilter filter, UserCountQueries.Counter? countedElsewhere = null)
    {
        // Everything below reads off the filter directly, rather than aliasing
        // fifteen locals first and rebuilding the positional list this record
        // exists to remove. The search term is the exception: nullable-flow
        // analysis narrows a local, not a property.
        var search = filter.Search;

        // Exclude removed users and system users from public lists
        IQueryable<User> query = _dbContext.Users.Where(u => u.Role != UserRole.System);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = LikePatterns.Contains(search);
            var searchLower = search.ToLower();

            // Fuzzy search: contains match OR trigram similarity > threshold
            // This finds both exact/partial matches and typo-tolerant matches
            query = query.Where(u =>
                EF.Functions.ILike(u.Username, searchPattern) ||
                EF.Functions.TrigramsSimilarity(u.Username, searchLower) > LikePatterns.TrigramThreshold ||
                u.UsernameHistories.Any(h =>
                    EF.Functions.ILike(h.OldUsername, searchPattern) ||
                    EF.Functions.TrigramsSimilarity(h.OldUsername, searchLower) > LikePatterns.TrigramThreshold));
        }

        var now = _dateTimeProvider.Now;
        var activeRange = now - ActivityPolicy.ActivePeriod;
        var onlineRange = now - ActivityPolicy.OnlinePeriod;

        if (filter.Activity == UserActivityFilter.Active)
        {
            query = query.Where(u => u.LastActivityUtc.HasValue && u.LastActivityUtc > activeRange);
        }
        else if (filter.Activity == UserActivityFilter.Inactive)
        {
            query = query.Where(u => !u.LastActivityUtc.HasValue || u.LastActivityUtc <= activeRange);
        }

        // Online filter: users active in last 5 minutes
        if (filter.IsOnline == true)
        {
            query = query.Where(u => u.LastActivityUtc.HasValue && u.LastActivityUtc > onlineRange);
        }

        if (filter.Role.HasValue)
        {
            query = query.Where(u => u.Role == filter.Role.Value);
        }

        // The stored column, which is the same rule compiled into the schema, so
        // the filter and the badge on the profile cannot answer differently.
        if (filter.IsNewbie.HasValue)
        {
            query = query.Where(u => u.IsNewbie == filter.IsNewbie.Value);
        }

        // Rating filter (based on QualityRating = post review score sum)
        if (filter.MinRating.HasValue)
        {
            query = query.Where(u => !u.RatingDisabled && u.QualityRating >= filter.MinRating.Value);
        }
        if (filter.MaxRating.HasValue)
        {
            query = query.Where(u => !u.RatingDisabled && u.QualityRating <= filter.MaxRating.Value);
        }

        // Registration date range filter
        if (filter.RegisteredFromUtc.HasValue)
        {
            query = query.Where(u => u.CreatedUtc >= filter.RegisteredFromUtc.Value);
        }
        if (filter.RegisteredToUtc.HasValue)
        {
            query = query.WhereAtOrBefore(u => u.CreatedUtc, filter.RegisteredToUtc.Value);
        }

        // The three numeric ranges, over the same counters the sorts order by and
        // through the same join: a Count() correlated to the user row is evaluated
        // per row of Users, and these filters are what the page is selected by.
        // Min and max share one join — the bound is a predicate over the number,
        // not a second reason to count.
        foreach (var counter in new[]
                 {
                     UserCountQueries.Counter.GamesHosting,
                     UserCountQueries.Counter.GamesPlaying,
                     UserCountQueries.Counter.BlogsHosting,
                 })
        {
            if (counter == countedElsewhere)
            {
                continue;
            }

            var (min, max) = RangeOf(filter, counter);
            if (min.HasValue || max.HasValue)
            {
                query = UserCountQueries.InRange(
                    UserCountQueries.Counted(_dbContext, counter, query,
                        _dateTimeProvider.Now - ActivityPolicy.ActivePeriod),
                    min, max);
            }
        }

        return query;
    }
}
