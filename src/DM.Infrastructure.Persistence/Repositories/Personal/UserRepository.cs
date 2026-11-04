using DM.Domain.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Core.Chats;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Repositories.Account;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;

using DM.Infrastructure.Persistence.Shared.Users;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc />
internal class UserRepository : IUserRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

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
        // Read once each: the ordering below branches on them a dozen times.
        var search = filter.Search;
        var sort = filter.Sort;
        var sortAscending = filter.SortAscending;

        // The four sorts over a derived number; null for every other sort, which orders
        // by a column of Users and needs no join at all.
        var sortCounter = CounterOf(sort);

        // No Include: the query ends in a projection, which makes EF drop every Include.
        //
        // The range over the counter this page is sorted by is left out here and applied
        // to the join below: filtering and ordering by the same number through two joins
        // put two GROUP BYs over the same table into one statement, and "min games
        // hosting, sorted by games hosting" is the ordinary way that filter is used.
        var baseQuery = GetQuery(filter, sortCounter);

        IQueryable<User> pageQuery;

        // The page left-joined to one GROUP BY per counted table, so the aggregate runs
        // once per request instead of once per row of Users.
        IQueryable<UserCountQueries.CountedUser>? counted = null;
        if (sortCounter.HasValue)
        {
            counted = UserCountQueries.Counted(_dbContext, sortCounter.Value, baseQuery,
                _dateTimeProvider.Now - ActivityPolicy.ActivePeriod);
            var (min, max) = RangeOf(filter, sortCounter.Value);
            counted = UserCountQueries.InRangeCounted(counted, min, max);
        }

        // When searching without explicit sort, use relevance-based ordering
        // When user explicitly selects a sort (e.g., rating), respect their choice
        if (!string.IsNullOrWhiteSpace(search) && sort == UserSort.Name)
        {
            // Default sort + search = use relevance ranking
            var searchLower = search.ToLower();
            // Relevance ranking:
            // 1. Exact match (highest)
            // 2. Prefix match
            // 3. Similarity score (for fuzzy matches)
            // 4. Alphabetically within same relevance, in the direction asked for
            //
            // The direction reaches the last key and no further. Relevance is what
            // a search is for, so it leads whatever the caller asked; but the tie
            // among equally relevant names is theirs to order, and the branch used
            // to drop sortOrder on the floor — the list accepted the parameter,
            // answered 200 and came back in the same order either way.
            var prefixPattern = LikePatterns.StartsWith(search);
            var relevance = baseQuery
                .OrderByDescending(u => u.Username.ToLower() == searchLower)
                .ThenByDescending(u => EF.Functions.ILike(u.Username, prefixPattern))
                .ThenByDescending(u => EF.Functions.TrigramsSimilarity(u.Username, searchLower));

            pageQuery = (sortAscending
                    ? relevance.ThenBy(u => u.Username).ThenBy(u => u.UserId)
                    : relevance.ThenByDescending(u => u.Username).ThenByDescending(u => u.UserId))
                .Page(paging);
        }
        else if (counted != null)
        {
            pageQuery = UserCountQueries.Order(counted, sortAscending)
                .Page(paging)
                .Select(x => x.User);
        }
        else
        {
            // Explicit sort selected or no search - use specified sort with direction
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
            pageQuery = (sortAscending
                    ? orderedQuery.ThenBy(u => u.UserId)
                    : orderedQuery.ThenByDescending(u => u.UserId))
                .Page(paging);
        }

        return pageQuery;
    }

    /// <inheritdoc />
    public async Task<GeneralUser?> GetUserAsync(string username)
    {
        var user = await _dbContext.Users
            .Where(AccountNames.UsernameIs(username))
            .ProjectToGeneralUser()
            .FirstOrDefaultAsync();

        if (user != null)
        {
            await PopulateProfileCounters(new[] { user });
        }

        return user;
    }

    /// <inheritdoc />
    public Task<Guid?> FindUserIdAsync(string username) =>
        _dbContext.Users
            .TagWith("DM.User.FindId")
            .Where(AccountNames.UsernameIs(username))
            .Select(u => (Guid?)u.UserId)
            .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<GeneralUser?> GetUserAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .Where(u => u.UserId == userId)
            .ProjectToGeneralUser()
            .FirstOrDefaultAsync();

        if (user != null)
        {
            await PopulateProfileCounters(new[] { user });
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<UserDetails?> GetUserDetailsAsync(string username)
    {
        var userDetails = await _dbContext.Users
            .Where(AccountNames.UsernameIs(username))
            .ProjectToUserDetails()
            .FirstOrDefaultAsync();

        if (userDetails == null)
        {
            return null;
        }

        await PopulateProfileCounters(new[] { userDetails });

        userDetails.Settings = await ReadSettings(userDetails.UserId);
        return userDetails;
    }

    /// <inheritdoc />
    public async Task<UserDetails?> GetUserDetailsAsync(Guid userId)
    {
        var userDetails = await _dbContext.Users
            .Where(u => u.UserId == userId)
            .ProjectToUserDetails()
            .FirstOrDefaultAsync();

        if (userDetails == null)
        {
            return null;
        }

        await PopulateProfileCounters(new[] { userDetails });

        userDetails.Settings = await ReadSettings(userDetails.UserId);
        return userDetails;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsersByRoleAsync(UserRole role)
    {
        var users = await _dbContext.Users
            .Where(u => u.Role == role)
            .OrderBy(u => u.Username)
            .ProjectToGeneralUser()
            .ToArrayAsync();

        await PopulateProfileCounters(users);
        return users;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsersAsync(IEnumerable<Guid> userIds)
    {
        var idsList = userIds.ToList();
        if (!idsList.Any())
        {
            return Array.Empty<GeneralUser>();
        }

        var users = await _dbContext.Users
            .Where(u => idsList.Contains(u.UserId))
            .ProjectToGeneralUser()
            .ToArrayAsync();

        await PopulateProfileCounters(users);
        return users;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserReference>> GetUserReferencesAsync(IEnumerable<Guid> userIds)
    {
        var idsList = userIds.ToList();
        if (idsList.Count == 0)
        {
            return Array.Empty<UserReference>();
        }

        // Written out rather than through ProjectToGeneralUser, to select five columns only.
        return await _dbContext.Users
            .TagWith("DM.User.References")
            .Where(u => idsList.Contains(u.UserId))
            .Select(u => new UserReference
            {
                UserId = u.UserId,
                Username = u.Username,
                LastActivityUtc = u.LastActivityUtc,
                Role = u.Role,
                IsNewbie = u.IsNewbie,
            })
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<Guid>> GetRemovedAccountIdsAsync() =>
        RemovedAccountScope.Ids(_dbContext.Users);

    /// <inheritdoc />
    /// <remarks>
    /// The games and blogs are counted here rather than added up from
    /// <see cref="PopulateListCounts" /> per account: a game with a removed master and
    /// a removed assistant, or with two removed players, would count twice, and the
    /// number on the tab would stop being the length of the list behind it. Each game
    /// and blog counts once, by the same predicates the per-account counters use.
    /// </remarks>
    public async Task<GeneralUser> GetRemovedAccountSummaryAsync()
    {
        var removed = await RemovedAccountScope.Ids(_dbContext.Users);
        var (quality, quantity) = await RemovedAccountScope.RatingTotals(_dbContext.Users);

        var reviewsGiven = await _dbContext.PostReviews
            .TagWith("DM.User.RemovedAccountSummary")
            .CountAsync(r => removed.Contains(r.AuthorId));
        var reviewsReceived = await _dbContext.PostReviews
            .TagWith("DM.User.RemovedAccountSummary")
            .CountAsync(r => removed.Contains(r.PostAuthorId));

        var gamesHosting = await _dbContext.Games
            .TagWith("DM.User.RemovedAccountSummary")
            .Where(g => removed.Contains(g.MasterId) || g.Assistants.Any(a => removed.Contains(a.UserId)))
            .GroupBy(g => g.Status)
            .Select(g => new StatusCountItem(Guid.Empty, g.Key, g.Count()))
            .ToListAsync();
        var gamesPlaying = await _dbContext.Games
            .TagWith("DM.User.RemovedAccountSummary")
            .Where(g => g.Characters.Any(c =>
                !c.IsNpc && c.AuthorId.HasValue && removed.Contains(c.AuthorId.Value)))
            .GroupBy(g => g.Status)
            .Select(g => new StatusCountItem(Guid.Empty, g.Key, g.Count()))
            .ToListAsync();
        var blogsHosting = await _dbContext.Blogs
            .TagWith("DM.User.RemovedAccountSummary")
            .Where(b => removed.Contains(b.AuthorId) || b.Assistants.Any(a => removed.Contains(a.UserId)))
            .GroupBy(b => b.Status)
            .Select(g => new StatusCountItem(Guid.Empty, g.Key, g.Count()))
            .ToListAsync();

        var gamesHostingByStatus = BuildStatusBreakdownDict(gamesHosting, null).GetValueOrDefault(Guid.Empty);
        var gamesPlayingByStatus = BuildStatusBreakdownDict(gamesPlaying, null).GetValueOrDefault(Guid.Empty);
        var blogsHostingByStatus = BuildStatusBreakdownDict(blogsHosting, null).GetValueOrDefault(Guid.Empty);

        return new GeneralUser
        {
            UserId = Guid.Empty,
            Username = RemovedAccount.Name,
            Role = UserRole.RegularUser,
            QualityRating = quality,
            QuantityRating = quantity,
            PostReviewsGivenCount = reviewsGiven,
            PostReviewsReceivedCount = reviewsReceived,
            GamesHostingByStatus = gamesHostingByStatus,
            GamesHosting = gamesHostingByStatus?.Total ?? 0,
            GamesPlayingByStatus = gamesPlayingByStatus,
            GamesPlaying = gamesPlayingByStatus?.Total ?? 0,
            BlogsHostingByStatus = blogsHostingByStatus,
            BlogsHosting = blogsHostingByStatus?.Total ?? 0,
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// The settings write is load-modify-save with one retry on a lost insert
    /// race, and deliberately not a dynamic ON CONFLICT: the set of updated
    /// fields is decided by the caller field by field, and composing that
    /// statement dynamically costs more than repeating the existing retry
    /// semantics. The settings row has more than one writer (this method and
    /// the bot link), so the insert of a fresh row can lose; the loser reruns
    /// as an update of the row the winner created. Exempted by name in
    /// DurableWritesShould: the second write replays the first, it does not
    /// follow it.
    /// </remarks>
    public async Task UpdateUser(UpdateUserEntity userUpdate, UpdateUserSettingsEntity settingsUpdate)
    {
        var user = await _dbContext.Users.FindAsync(userUpdate.UserId);
        if (user == null) return;

        // Update user entity fields
        if (userUpdate.UpdateStatus) user.Status = userUpdate.Status;
        if (userUpdate.UpdateName) user.Name = userUpdate.Name;
        if (userUpdate.UpdateLocation) user.Location = userUpdate.Location;
        if (userUpdate.UpdateInfo) user.Info = userUpdate.Info;
        if (userUpdate.RatingDisabled?.Value is { } ratingDisabled) user.RatingDisabled = ratingDisabled;
        if (userUpdate.ShowBirthday?.Value is { } showBirthday) user.ShowBirthday = showBirthday;
        if (userUpdate.AvatarUploadId?.Value is { } avatarUploadId) user.AvatarUploadId = avatarUploadId;

        // The settings row travels in the same SaveChanges as the user fields:
        // one transaction, both or neither.
        var settings = await _dbContext.UserSettings.FindAsync(settingsUpdate.UserId);
        var inserting = settings == null;
        if (settings == null)
        {
            // A fresh row is the defaults with the caller's fields over them: a
            // partial row is unrepresentable (every paging column is NOT NULL),
            // so the rest has to be the default rather than empty.
            settings = UserSettings.CreateDefault(settingsUpdate.UserId);
            _dbContext.UserSettings.Add(settings);
        }

        ApplySettingsUpdate(settings, settingsUpdate);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (inserting && e.IsDuplicateKey())
        {
            // Another writer created the row between the read and the insert.
            // The whole SaveChanges rolled back, so the user fields are still
            // pending on the tracked entity; the settings insert becomes an
            // update of the winner's row.
            _dbContext.Entry(settings).State = EntityState.Detached;
            var existing = await _dbContext.UserSettings
                .TagWith("DM.User.Settings.RetryAfterLostInsert")
                .FirstAsync(s => s.UserId == settingsUpdate.UserId);
            ApplySettingsUpdate(existing, settingsUpdate);
            await _dbContext.SaveChangesAsync();
        }
    }

    private static void ApplySettingsUpdate(UserSettings settings, UpdateUserSettingsEntity settingsUpdate)
    {
        if (settingsUpdate.Theme?.Value is { } theme) settings.Theme = theme;
        // Blank is the same answer as absent - the reader asked for UTC - so it
        // is stored as absent and there is one spelling of "no zone" in the row.
        if (settingsUpdate.UpdateTimeZone)
            settings.TimeZoneId = string.IsNullOrWhiteSpace(settingsUpdate.TimeZoneId)
                ? null
                : settingsUpdate.TimeZoneId.Trim();
        if (settingsUpdate.PageSize?.Value is { } pageSize) settings.PageSize = pageSize;
    }


    private async Task<DM.Domain.Core.Identity.UserSettings> ReadSettings(Guid userId)
    {
        var userSettings = await _dbContext.UserSettings
            .TagWith("DM.User.Settings")
            .FirstOrDefaultAsync(s => s.UserId == userId);
        return userSettings == null
            ? DM.Domain.Core.Identity.UserSettings.Default
            : userSettings.ToUserSettings();
    }

    /// <inheritdoc />
    public async Task ReplaceUserContacts(Guid userId, IEnumerable<UserContactEntity> contacts)
    {
        // Remove existing contacts
        var existingContacts = await _dbContext.UserContacts
            .Where(c => c.UserId == userId)
            .ToListAsync();
        _dbContext.UserContacts.RemoveRange(existingContacts);

        // Add new contacts
        var newContacts = contacts.Select(c => new DbUserContact
        {
            UserContactId = c.UserContactId,
            UserId = c.UserId,
            ContactType = c.ContactType,
            ContactValue = c.ContactValue,
            SortOrder = c.SortOrder
        });
        _dbContext.UserContacts.AddRange(newContacts);

        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public Task<Guid?> GetConfirmedAvatarUpload(Guid userId, Guid uploadId) =>
        _dbContext.Uploads
            .Where(u => u.UploadId == uploadId &&
                        u.UserId == userId &&
                        u.Status == UploadStatus.Confirmed)
            .Select(u => (Guid?)u.UploadId)
            .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task LinkAvatarUpload(Guid userId, Guid uploadId)
    {
        var oldUploads = await _dbContext.Uploads
            .Where(u => u.UserId == userId &&
                        u.UploadId != uploadId &&
                        u.Type == UploadType.UserAvatar)
            .ToListAsync();

        foreach (var oldUpload in oldUploads)
        {
            // No author, and the moment starts the sweeper's grace period; see
            // UnlinkAvatarUpload below.
            SoftDelete.Mark(oldUpload, null, _dateTimeProvider.Now);
        }

        var user = await _dbContext.Users.FindAsync(userId);
        if (user != null)
        {
            user.AvatarUploadId = uploadId;
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UnlinkAvatarUpload(Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) return;
        if (user.AvatarUploadId == null) return; // Idempotent: no avatar already

        user.AvatarUploadId = null;

        // The objects stay for the sweeper's grace period.
        var avatarUploads = await _dbContext.Uploads
            .Where(u => u.UserId == userId && u.Type == UploadType.UserAvatar)
            .ToListAsync();

        foreach (var upload in avatarUploads)
        {
            SoftDelete.Mark(upload, null, _dateTimeProvider.Now);
        }

        await _dbContext.SaveChangesAsync();
    }

    private record StatusCountItem(Guid UserId, ModuleStatus Status, int Count);

    private static Dictionary<Guid, ModuleStatusCounts> BuildStatusBreakdownDict(
        IEnumerable<StatusCountItem> primary,
        IEnumerable<StatusCountItem>? secondary)
    {
        var combined = primary.ToList();
        if (secondary != null)
        {
            combined.AddRange(secondary);
        }

        return combined
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var counts = new ModuleStatusCounts();
                    foreach (var item in g)
                    {
                        switch (item.Status)
                        {
                            case ModuleStatus.Draft:
                                counts.Draft += item.Count;
                                break;
                            case ModuleStatus.Active:
                                counts.Active += item.Count;
                                break;
                            case ModuleStatus.Closed:
                                counts.Closed += item.Count;
                                break;
                        }
                    }
                    return counts;
                });
    }

    /// <summary>The counter a sort orders by, or null for a sort over a column of Users.</summary>
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

    /// <summary>
    /// The filtered set of users.
    /// </summary>
    /// <param name="filter">Everything the caller asked to narrow the list by.</param>
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

    /// <summary>
    /// The counters the user list draws: recommendations received, and the game and
    /// blog breakdowns behind its three numeric columns.
    /// </summary>
    /// <remarks>
    /// Split out of the profile enrichment because the list was paying for all of it —
    /// twenty-odd aggregates for a page of fifty, of which the table renders four.
    /// Post reviews, bans, drops, likes, subscribers and username history are
    /// profile content and are fetched by the profile. Game reviews received is
    /// the one review counter the list DOES carry: the community table renders
    /// it as a column.
    /// </remarks>
    private async Task PopulateListCounts(IEnumerable<GeneralUser> users)
    {
        var usersList = users.ToList();
        if (!usersList.Any())
        {
            return;
        }

        var userIds = usersList.Select(u => u.UserId).ToList();

        var endorsementsReceived = await CountByUser(_dbContext.UserEndorsements
            .Where(e => userIds.Contains(e.TargetUserId))
            .Select(e => e.TargetUserId));

        // Games hosting: user is master (count games where user is MasterId) - with status breakdown
        var gamesMasterByStatus = await _dbContext.Games
            .Where(g => userIds.Contains(g.MasterId))
            .GroupBy(g => new { g.MasterId, g.Status })
            .Select(g => new StatusCountItem(g.Key.MasterId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Games assistant: with status breakdown
        var gamesAssistantByStatus = await _dbContext.GameAssistants
            .Where(a => userIds.Contains(a.UserId) && !a.Game.IsRemoved)
            .GroupBy(a => new { a.UserId, a.Game.Status })
            .Select(g => new StatusCountItem(g.Key.UserId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Games playing: user has active character - with status breakdown
        var gamesPlayingByStatus = await _dbContext.Characters
            .Where(c => !c.IsNpc && c.AuthorId.HasValue && userIds.Contains(c.AuthorId.Value) && !c.Game.IsRemoved)
            .Select(c => new { AuthorId = c.AuthorId!.Value, c.Game.GameId, c.Game.Status })
            .Distinct()
            .GroupBy(c => new { c.AuthorId, c.Status })
            .Select(g => new StatusCountItem(g.Key.AuthorId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Blogs hosting: user is owner - with status breakdown
        var blogsOwnerByStatus = await _dbContext.Blogs
            .Where(b => userIds.Contains(b.AuthorId))
            .GroupBy(b => new { b.AuthorId, b.Status })
            .Select(g => new StatusCountItem(g.Key.AuthorId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Blogs assistant: with status breakdown
        var blogsAssistantByStatus = await _dbContext.BlogAssistants
            .Where(a => userIds.Contains(a.UserId) && !a.Blog.IsRemoved)
            .GroupBy(a => new { a.UserId, a.Blog.Status })
            .Select(g => new StatusCountItem(g.Key.UserId, g.Key.Status, g.Count()))
            .ToListAsync();

        // A game review received lands on the game's master, the relation
        // GameReviewFilter.GmId selects by, so the counter agrees with its list.
        var gameReviewsReceived = await CountByUser(_dbContext.GameReviews
            .Where(r => !r.Game.IsRemoved && userIds.Contains(r.Game.MasterId))
            .Select(r => r.Game.MasterId));

        // Build status breakdown dictionaries
        var gamesHostingByStatusDict = BuildStatusBreakdownDict(gamesMasterByStatus, gamesAssistantByStatus);
        var gamesPlayingByStatusDict = BuildStatusBreakdownDict(gamesPlayingByStatus, null);
        var blogsHostingByStatusDict = BuildStatusBreakdownDict(blogsOwnerByStatus, blogsAssistantByStatus);

        foreach (var user in usersList)
        {
            user.EndorsementsReceivedCount = endorsementsReceived.GetValueOrDefault(user.UserId);
            user.GameReviewsReceivedCount = gameReviewsReceived.GetValueOrDefault(user.UserId);

            // Games hosting = sum from status breakdown
            user.GamesHostingByStatus = gamesHostingByStatusDict.TryGetValue(user.UserId, out var gh) ? gh : null;
            user.GamesHosting = user.GamesHostingByStatus?.Total ?? 0;

            // Games playing = sum from status breakdown
            user.GamesPlayingByStatus = gamesPlayingByStatusDict.TryGetValue(user.UserId, out var gp) ? gp : null;
            user.GamesPlaying = user.GamesPlayingByStatus?.Total ?? 0;

            // Blogs hosting = sum from status breakdown
            user.BlogsHostingByStatus = blogsHostingByStatusDict.TryGetValue(user.UserId, out var bh) ? bh : null;
            user.BlogsHosting = user.BlogsHostingByStatus?.Total ?? 0;
        }
    }

    private async Task PopulateProfileCounters(IEnumerable<GeneralUser> users)
    {
        var usersList = users.ToList();
        if (!usersList.Any())
        {
            return;
        }

        var userIds = usersList.Select(u => u.UserId).ToList();

        // Everything the list needs is the first part of everything the profile needs.
        await PopulateListCounts(usersList);

        // Sequential: DbContext is not thread-safe.
        var given = await CountByUser(_dbContext.PostReviews
            .Where(r => userIds.Contains(r.AuthorId))
            .Select(r => r.AuthorId));

        var received = await CountByUser(_dbContext.PostReviews
            .Where(r => userIds.Contains(r.PostAuthorId))
            .Select(r => r.PostAuthorId));

        var endorsementsGiven = await CountByUser(_dbContext.UserEndorsements
            .Where(e => userIds.Contains(e.AuthorId))
            .Select(e => e.AuthorId));

        var gameReviewsGiven = await CountByUser(_dbContext.GameReviews
            .Where(r => userIds.Contains(r.AuthorId))
            .Select(r => r.AuthorId));

        var topicsAuthored = await CountByUser(_dbContext.Topics
            .Where(t => userIds.Contains(t.AuthorId))
            .Select(t => t.AuthorId));

        var commentsAuthored = await CountByUser(_dbContext.Comments
            .Where(c => userIds.Contains(c.AuthorId))
            .Select(c => c.AuthorId));

        var globalChatId = WellKnownChats.GlobalChatId;
        var globalChatMessages = await CountByUser(_dbContext.Messages
            .Where(m => m.ChatId == globalChatId && userIds.Contains(m.UserId))
            .Select(m => m.UserId));

        // A ban lifted early was taken back: the count is of bans a user served.
        var bansReceived = await CountByUser(_dbContext.Bans
            .Where(b => userIds.Contains(b.TargetUserId) && b.LiftedUtc == null)
            .Select(b => b.TargetUserId));

        // A drop is a player leaving on their own: a retired non-NPC with IsPlayerLeft.
        // Death and exile by the master are not drops.
        var gameDrops = await CountByUser(_dbContext.Characters
            .Where(c => c.AuthorId.HasValue
                && userIds.Contains(c.AuthorId.Value)
                && !c.IsNpc
                && !c.Game.IsRemoved
                && c.Status == CharacterStatus.Retired
                && c.IsPlayerLeft)
            .Select(c => c.AuthorId!.Value));

        // Drafts count too.
        var publicationsAuthored = await CountByUser(_dbContext.Publications
            .Where(p => userIds.Contains(p.AuthorId))
            .Select(p => p.AuthorId));

        // Likes are polymorphic, so one join per kind of content, summed below. Game
        // posts are not counted: their quality signal is the rating.
        var likesOnTopics = await CountByUser(_dbContext.Likes
            .Where(l => l.EntityType == LikeEntityType.Topic)
            .Join(_dbContext.Topics.Where(t => userIds.Contains(t.AuthorId)),
                l => l.EntityId, t => t.TopicId, (l, t) => t.AuthorId));

        var likesOnPublications = await CountByUser(_dbContext.Likes
            .Where(l => l.EntityType == LikeEntityType.Publication)
            .Join(_dbContext.Publications.Where(p => userIds.Contains(p.AuthorId)),
                l => l.EntityId, p => p.PublicationId, (l, p) => p.AuthorId));

        var likesOnComments = await CountByUser(_dbContext.Likes
            .Where(l => l.EntityType == LikeEntityType.Comment)
            .Join(_dbContext.Comments.Where(c => userIds.Contains(c.AuthorId)),
                l => l.EntityId, c => c.CommentId, (l, c) => c.AuthorId));

        var likesOnMessages = await CountByUser(_dbContext.Likes
            .Where(l => l.EntityType == LikeEntityType.Message)
            .Join(_dbContext.Messages.Where(m => userIds.Contains(m.UserId)),
                l => l.EntityId, m => m.MessageId, (l, m) => m.UserId));

        // Subscribers: the total and a capped preview taken in SQL (ROW_NUMBER per target). The
        // Join keeps both over the same rows: the filter of the subscriber reaches it.
        var subscriberSummaries = await _dbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.User && userIds.Contains(s.TargetId))
            .Join(_dbContext.Users, s => s.SubscriberId, u => u.UserId,
                (s, u) => new
                {
                    s.TargetId,
                    s.SubscriptionId,
                    s.SubscriberId,
                    s.Settings,
                    u.Username,
                    u.LastActivityUtc,
                })
            .GroupBy(x => x.TargetId)
            .Select(g => new
            {
                UserId = g.Key,
                // One count per profile line. The preview below is capped before
                // anything knows about categories, so a line drawn from it can be
                // short or empty while the category is full; these are what the
                // line reports. Npgsql turns each into
                // count(*) FILTER (WHERE "Settings" & <bit> <> 0) inside the
                // GROUP BY that is already running over exactly these rows —
                // verified against the generated SQL, so no extra statement and
                // no correlated subquery. Not distinct-counted: two rows for one
                // subscriber is a race artefact, and a category count that
                // disagreed with a distinct total would be the worse lie.
                GameSubscribers = g.Count(x =>
                    (x.Settings & SubscriptionSettings.AuthorGameEvents) != 0),
                BlogSubscribers = g.Count(x =>
                    (x.Settings & SubscriptionSettings.AuthorBlogEvents) != 0),
                TopicSubscribers = g.Count(x =>
                    (x.Settings & SubscriptionSettings.AuthorTopicEvents) != 0),
                // Never-active subscribers sort last; a plain DESC in Postgres
                // would put their nulls first.
                Preview = g.OrderByDescending(x => x.LastActivityUtc != null)
                    .ThenByDescending(x => x.LastActivityUtc)
                    .ThenBy(x => x.SubscriptionId)
                    .Take(SubscriptionPolicy.PreviewCap)
                    .Select(x => new SubscriberInfo
                    {
                        Username = x.Username,
                        LastActivityUtc = x.LastActivityUtc,
                        Settings = x.Settings,
                    })
                    .ToList(),
            })
            .ToDictionaryAsync(x => x.UserId);

        // Username history: fetch and group by user
        var usernameHistoryData = await _dbContext.UsernameHistories
            .Where(h => userIds.Contains(h.UserId))
            .OrderByDescending(h => h.ChangedUtc)
            .SelectSpliced(h => new { h.UserId, Entry = UsernameHistoryRepository.Projection.Splice(h) })
            .ToListAsync();

        var usernameHistoryDict = usernameHistoryData
            .GroupBy(h => h.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Entry).ToList());

        foreach (var user in usersList)
        {
            user.PostReviewsGivenCount = given.GetValueOrDefault(user.UserId);
            user.PostReviewsReceivedCount = received.GetValueOrDefault(user.UserId);
            user.EndorsementsGivenCount = endorsementsGiven.GetValueOrDefault(user.UserId);
            user.GameReviewsGivenCount = gameReviewsGiven.GetValueOrDefault(user.UserId);
            user.TopicsAuthoredCount = topicsAuthored.GetValueOrDefault(user.UserId);
            user.CommentsAuthoredCount = commentsAuthored.GetValueOrDefault(user.UserId);
            user.GlobalChatMessagesCount = globalChatMessages.GetValueOrDefault(user.UserId);
            user.BansReceivedCount = bansReceived.GetValueOrDefault(user.UserId);
            user.GameDropsCount = gameDrops.GetValueOrDefault(user.UserId);
            user.PublicationsAuthoredCount = publicationsAuthored.GetValueOrDefault(user.UserId);
            user.LikesReceivedCount = likesOnTopics.GetValueOrDefault(user.UserId) +
                                      likesOnPublications.GetValueOrDefault(user.UserId) +
                                      likesOnComments.GetValueOrDefault(user.UserId) +
                                      likesOnMessages.GetValueOrDefault(user.UserId);

            var subscribers = subscriberSummaries.GetValueOrDefault(user.UserId);
            user.Subscribers = subscribers?.Preview ?? [];
            user.SubscribersByCategory = new SubscribersByCategory
            {
                Games = subscribers?.GameSubscribers ?? 0,
                Blogs = subscribers?.BlogSubscribers ?? 0,
                Topics = subscribers?.TopicSubscribers ?? 0,
            };
            user.UsernameHistory = usernameHistoryDict.TryGetValue(user.UserId, out var history) ? history : [];
        }
    }

    /// <summary>
    /// How many rows each user owns, in one GROUP BY; a user without rows is absent.
    /// </summary>
    private static Task<Dictionary<Guid, int>> CountByUser(IQueryable<Guid> userIdOfEachRow) =>
        userIdOfEachRow
            .GroupBy(userId => userId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);
}
