using DM.Domain.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.Profiles;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc />
internal class UserRepository : MongoCollectionRepository<UserSettings>, IUserRepository
{
    private readonly DmDbContext _dmDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMapper _mapper;


    /// <inheritdoc />
    public UserRepository(
        DmDbContext dmDbContext,
        DmMongoClient mongoClient,
        IDateTimeProvider dateTimeProvider,
        IMapper mapper) : base(mongoClient)
    {
        _dmDbContext = dmDbContext;
        _dateTimeProvider = dateTimeProvider;
        _mapper = mapper;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> CountUsersAsync(UserFilter filter) => GetQuery(filter).CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsersAsync(PagingData paging, UserFilter filter)
    {
        var users = await BuildPageQuery(paging, filter)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
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
    /// <c>ToQueryString</c> and from nothing else — and because ProjectTo needs a
    /// configured mapper while this needs nothing but the context.
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

        // No Include: the query ends in ProjectTo, which builds its own Select and makes
        // EF drop every Include with a warning. The avatar comes from the mapping
        // expression and the username history from its own statement below.
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
            counted = UserCountQueries.Counted(_dmDbContext, sortCounter.Value, baseQuery,
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
            var relevance = baseQuery
                .OrderByDescending(u => u.Username.ToLower() == searchLower)
                .ThenByDescending(u => EF.Functions.ILike(u.Username, search + "%"))
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
        var user = await _dmDbContext.Users
            .Where(u => !u.IsRemoved && u.Username.ToLower() == username.ToLower())
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (user != null)
        {
            await PopulatePostReviewCounts(new[] { user });
        }

        return user;
    }

    /// <inheritdoc />
    public Task<Guid?> FindUserIdAsync(string username) =>
        _dmDbContext.Users
            .TagWith("DM.User.FindId")
            .Where(u => !u.IsRemoved && u.Username.ToLower() == username.ToLower())
            .Select(u => (Guid?)u.UserId)
            .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<GeneralUser?> GetUserAsync(Guid userId)
    {
        var user = await _dmDbContext.Users
            .Where(u => !u.IsRemoved && u.UserId == userId)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (user != null)
        {
            await PopulatePostReviewCounts(new[] { user });
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<UserDetails?> GetUserDetailsAsync(string username)
    {
        var userDetails = await _dmDbContext.Users
            .Where(u => !u.IsRemoved && u.Username.ToLower() == username.ToLower())
            .ProjectTo<UserDetails>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (userDetails == null)
        {
            return null;
        }

        await PopulatePostReviewCounts(new[] { userDetails });

        var userSettings = await Collection
            .Find(Filter.Eq(u => u.UserId, userDetails.UserId))
            .FirstOrDefaultAsync();
        userDetails.Settings = userSettings == null
            ? DM.Domain.Core.Identity.UserSettings.Default
            : _mapper.Map<DM.Domain.Core.Identity.UserSettings>(userSettings);
        return userDetails;
    }

    /// <inheritdoc />
    public async Task<UserDetails?> GetUserDetailsAsync(Guid userId)
    {
        var userDetails = await _dmDbContext.Users
            .Where(u => !u.IsRemoved && u.UserId == userId)
            .ProjectTo<UserDetails>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (userDetails == null)
        {
            return null;
        }

        await PopulatePostReviewCounts(new[] { userDetails });

        var userSettings = await Collection
            .Find(Filter.Eq(u => u.UserId, userDetails.UserId))
            .FirstOrDefaultAsync();
        userDetails.Settings = userSettings == null
            ? DM.Domain.Core.Identity.UserSettings.Default
            : _mapper.Map<DM.Domain.Core.Identity.UserSettings>(userSettings);
        return userDetails;
    }

    /// <inheritdoc />
    public async Task<UserDetails?> GetUserDetailsByEmail(string email)
    {
        var userDetails = await _dmDbContext.Users
            .Where(u => !u.IsRemoved && u.Email.ToLower() == email.ToLower())
            .ProjectTo<UserDetails>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (userDetails == null)
        {
            return null;
        }

        await PopulatePostReviewCounts(new[] { userDetails });

        var userSettings = await Collection
            .Find(Filter.Eq(u => u.UserId, userDetails.UserId))
            .FirstOrDefaultAsync();
        userDetails.Settings = userSettings == null
            ? DM.Domain.Core.Identity.UserSettings.Default
            : _mapper.Map<DM.Domain.Core.Identity.UserSettings>(userSettings);
        return userDetails;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsersByRoleAsync(UserRole role)
    {
        var users = await _dmDbContext.Users
            .Where(u => !u.IsRemoved && u.Role == role)
            .OrderBy(u => u.Username)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        await PopulatePostReviewCounts(users);
        return users;
    }

    /// <inheritdoc />
    public Task<int> GetPostReviewsGivenCount(Guid userId) =>
        _dmDbContext.PostReviews
            .Where(r => r.AuthorId == userId && !r.IsRemoved)
            .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsersAsync(IEnumerable<Guid> userIds)
    {
        var idsList = userIds.ToList();
        if (!idsList.Any())
        {
            return Array.Empty<GeneralUser>();
        }

        var users = await _dmDbContext.Users
            .Where(u => !u.IsRemoved && idsList.Contains(u.UserId))
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        await PopulatePostReviewCounts(users);
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

        // Written out rather than routed through ProjectTo: the mapping to
        // GeneralUser is what drags the counter properties along, and the point
        // here is to select five columns and stop.
        return await _dmDbContext.Users
            .TagWith("DM.User.References")
            .Where(u => !u.IsRemoved && idsList.Contains(u.UserId))
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

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task UpdateUser(UpdateUserEntity userUpdate, UpdateUserSettingsEntity settingsUpdate)
    {
        var user = await _dmDbContext.Users.FindAsync(userUpdate.UserId);
        if (user == null) return;

        // Update user entity fields
        if (userUpdate.UpdateStatus) user.Status = userUpdate.Status;
        if (userUpdate.UpdateName) user.Name = userUpdate.Name;
        if (userUpdate.UpdateLocation) user.Location = userUpdate.Location;
        if (userUpdate.UpdateInfo) user.Info = userUpdate.Info;
        if (userUpdate.RatingDisabled?.Value.HasValue == true) user.RatingDisabled = userUpdate.RatingDisabled.Value.Value;
        if (userUpdate.ShowBirthday?.Value.HasValue == true) user.ShowBirthday = userUpdate.ShowBirthday.Value.Value;
        if (userUpdate.AvatarUploadId?.Value.HasValue == true) user.AvatarUploadId = userUpdate.AvatarUploadId.Value;

        await _dmDbContext.SaveChangesAsync();

        // Update user settings in MongoDB
        var filter = Builders<UserSettings>.Filter.Eq(s => s.UserId, settingsUpdate.UserId);
        var existingSettings = await Collection.Find(filter).FirstOrDefaultAsync();

        // A document written before Paging was mandatory, or by a caller that
        // left it out, has Paging: null — and Mongo cannot create a field
        // inside a null element, so the per-field $set below fails the whole
        // update with a 500. Such a document is repaired to defaults first and
        // then updated normally.
        if (existingSettings is { Paging: null })
        {
            await Collection.UpdateOneAsync(filter,
                Builders<UserSettings>.Update.Set(s => s.Paging,
                    UserSettings.CreateDefault(settingsUpdate.UserId).Paging));
        }

        if (existingSettings == null)
        {
            var newSettings = UserSettings.CreateDefault(settingsUpdate.UserId);
            newSettings.Theme = settingsUpdate.Theme?.Value ?? newSettings.Theme;
            newSettings.Paging.CommentsPerPage =
                settingsUpdate.CommentsPerPage?.Value ?? newSettings.Paging.CommentsPerPage;
            newSettings.Paging.TopicsPerPage =
                settingsUpdate.TopicsPerPage?.Value ?? newSettings.Paging.TopicsPerPage;
            newSettings.Paging.MessagesPerPage =
                settingsUpdate.MessagesPerPage?.Value ?? newSettings.Paging.MessagesPerPage;
            newSettings.Paging.PostsPerPage =
                settingsUpdate.PostsPerPage?.Value ?? newSettings.Paging.PostsPerPage;
            newSettings.Paging.EntitiesPerPage =
                settingsUpdate.EntitiesPerPage?.Value ?? newSettings.Paging.EntitiesPerPage;
            await Collection.InsertOneAsync(newSettings);
        }
        else
        {
            // Update existing settings
            var updateDefinitions = new List<UpdateDefinition<UserSettings>>();
            if (settingsUpdate.Theme?.Value.HasValue == true)
                updateDefinitions.Add(Builders<UserSettings>.Update.Set(s => s.Theme, settingsUpdate.Theme.Value.Value));
            if (settingsUpdate.CommentsPerPage?.Value.HasValue == true)
                updateDefinitions.Add(Builders<UserSettings>.Update.Set(s => s.Paging.CommentsPerPage, settingsUpdate.CommentsPerPage.Value.Value));
            if (settingsUpdate.TopicsPerPage?.Value.HasValue == true)
                updateDefinitions.Add(Builders<UserSettings>.Update.Set(s => s.Paging.TopicsPerPage, settingsUpdate.TopicsPerPage.Value.Value));
            if (settingsUpdate.MessagesPerPage?.Value.HasValue == true)
                updateDefinitions.Add(Builders<UserSettings>.Update.Set(s => s.Paging.MessagesPerPage, settingsUpdate.MessagesPerPage.Value.Value));
            if (settingsUpdate.PostsPerPage?.Value.HasValue == true)
                updateDefinitions.Add(Builders<UserSettings>.Update.Set(s => s.Paging.PostsPerPage, settingsUpdate.PostsPerPage.Value.Value));
            if (settingsUpdate.EntitiesPerPage?.Value.HasValue == true)
                updateDefinitions.Add(Builders<UserSettings>.Update.Set(s => s.Paging.EntitiesPerPage, settingsUpdate.EntitiesPerPage.Value.Value));

            if (updateDefinitions.Any())
            {
                var combinedUpdate = Builders<UserSettings>.Update.Combine(updateDefinitions);
                await Collection.UpdateOneAsync(filter, combinedUpdate);
            }
        }
    }

    /// <inheritdoc />
    public async Task ReplaceUserContacts(Guid userId, IEnumerable<UserContactEntity> contacts)
    {
        // Remove existing contacts
        var existingContacts = await _dmDbContext.UserContacts
            .Where(c => c.UserId == userId)
            .ToListAsync();
        _dmDbContext.UserContacts.RemoveRange(existingContacts);

        // Add new contacts
        var newContacts = contacts.Select(c => new Infrastructure.Persistence.Entities.Account.UserContact
        {
            UserContactId = c.UserContactId,
            UserId = c.UserId,
            ContactType = c.ContactType,
            ContactValue = c.ContactValue,
            SortOrder = c.SortOrder
        });
        _dmDbContext.UserContacts.AddRange(newContacts);

        await _dmDbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<Guid?> GetConfirmedAvatarUpload(Guid userId, Guid uploadId)
    {
        var upload = await _dmDbContext.Uploads
            .Where(u => u.UploadId == uploadId &&
                        u.UserId == userId &&
                        u.Status == UploadStatus.Confirmed &&
                        !u.IsRemoved)
            .Select(u => (Guid?)u.UploadId)
            .FirstOrDefaultAsync();

        return upload;
    }

    /// <inheritdoc />
    public async Task LinkAvatarUpload(Guid userId, Guid uploadId)
    {
        // Mark old avatar uploads by setting status to Replaced
        var oldUploads = await _dmDbContext.Uploads
            .Where(u => u.UserId == userId &&
                        u.UploadId != uploadId &&
                        !u.IsRemoved &&
                        u.Type == UploadType.UserAvatar)
            .ToListAsync();

        foreach (var oldUpload in oldUploads)
        {
            oldUpload.IsRemoved = true;
            // Starts the sweeper's grace period; see UnlinkAvatarUpload below.
            oldUpload.DeletedUtc = _dateTimeProvider.Now;
        }

        // Link new upload to user
        var user = await _dmDbContext.Users.FindAsync(userId);
        if (user != null)
        {
            user.AvatarUploadId = uploadId;
        }

        await _dmDbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UnlinkAvatarUpload(Guid userId)
    {
        var user = await _dmDbContext.Users.FindAsync(userId);
        if (user == null) return;
        if (user.AvatarUploadId == null) return; // Idempotent: no avatar already

        user.AvatarUploadId = null;

        // Soft-delete all UserAvatar-type Upload records of this user.
        // The GC worker (UploadOrphanCleanupService) sweeps the S3 objects after the grace period.
        var avatarUploads = await _dmDbContext.Uploads
            .Where(u => u.UserId == userId
                && u.Type == UploadType.UserAvatar
                && !u.IsRemoved)
            .ToListAsync();

        foreach (var upload in avatarUploads)
        {
            upload.IsRemoved = true;
            upload.DeletedUtc = _dateTimeProvider.Now;
        }

        await _dmDbContext.SaveChangesAsync();
    }

    // ═══ PRIVATE ═══

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

        IQueryable<User> query = filter.Activity switch
        {
            UserActivityFilter.Pending => _dmDbContext.Users.Where(u => false),
            // Exclude removed users and system users from public lists
            _ => _dmDbContext.Users.Where(u => !u.IsRemoved && u.Role != UserRole.System)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = "%" + search.Replace("%", "\\%").Replace("_", "\\_") + "%";
            var searchLower = search.ToLower();

            // Fuzzy search: contains match OR trigram similarity > threshold
            // This finds both exact/partial matches and typo-tolerant matches
            query = query.Where(u =>
                EF.Functions.ILike(u.Username, searchPattern) ||
                EF.Functions.TrigramsSimilarity(u.Username, searchLower) > 0.3 ||
                u.UsernameHistories.Any(h =>
                    EF.Functions.ILike(h.OldUsername, searchPattern) ||
                    EF.Functions.TrigramsSimilarity(h.OldUsername, searchLower) > 0.3));
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
                    UserCountQueries.Counted(_dmDbContext, counter, query,
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
    /// Reviews, bans, drops, likes, subscribers and username history are profile
    /// content and are fetched by the profile.
    /// </remarks>
    private async Task PopulateListCounts(IEnumerable<GeneralUser> users)
    {
        var usersList = users.ToList();
        if (!usersList.Any())
        {
            return;
        }

        var userIds = usersList.Select(u => u.UserId).ToList();

        var endorsementsReceivedCounts = await _dmDbContext.UserEndorsements
            .Where(e => userIds.Contains(e.TargetUserId) && !e.IsRemoved)
            .GroupBy(e => e.TargetUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Games hosting: user is master (count games where user is MasterId) - with status breakdown
        var gamesMasterByStatus = await _dmDbContext.Games
            .Where(g => !g.IsRemoved && userIds.Contains(g.MasterId))
            .GroupBy(g => new { g.MasterId, g.Status })
            .Select(g => new StatusCountItem(g.Key.MasterId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Games assistant: with status breakdown
        var gamesAssistantByStatus = await _dmDbContext.Set<Entities.Game.Links.GameAssistant>()
            .Where(a => userIds.Contains(a.UserId) && !a.Game.IsRemoved)
            .GroupBy(a => new { a.UserId, a.Game.Status })
            .Select(g => new StatusCountItem(g.Key.UserId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Games playing: user has active character - with status breakdown
        var gamesPlayingByStatus = await _dmDbContext.Characters
            .Where(c => !c.IsRemoved && !c.IsNpc && c.AuthorId.HasValue && userIds.Contains(c.AuthorId.Value) && !c.Game.IsRemoved)
            .Select(c => new { AuthorId = c.AuthorId!.Value, c.Game.GameId, c.Game.Status })
            .Distinct()
            .GroupBy(c => new { c.AuthorId, c.Status })
            .Select(g => new StatusCountItem(g.Key.AuthorId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Blogs hosting: user is owner - with status breakdown
        var blogsOwnerByStatus = await _dmDbContext.Blogs
            .Where(b => !b.IsRemoved && userIds.Contains(b.AuthorId))
            .GroupBy(b => new { b.AuthorId, b.Status })
            .Select(g => new StatusCountItem(g.Key.AuthorId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Blogs assistant: with status breakdown
        var blogsAssistantByStatus = await _dmDbContext.Set<Entities.Blog.BlogAssistant>()
            .Where(a => userIds.Contains(a.UserId) && !a.Blog.IsRemoved)
            .GroupBy(a => new { a.UserId, a.Blog.Status })
            .Select(g => new StatusCountItem(g.Key.UserId, g.Key.Status, g.Count()))
            .ToListAsync();

        var endorsementsReceivedDict = endorsementsReceivedCounts.ToDictionary(x => x.UserId, x => x.Count);

        // Build status breakdown dictionaries
        var gamesHostingByStatusDict = BuildStatusBreakdownDict(gamesMasterByStatus, gamesAssistantByStatus);
        var gamesPlayingByStatusDict = BuildStatusBreakdownDict(gamesPlayingByStatus, null);
        var blogsHostingByStatusDict = BuildStatusBreakdownDict(blogsOwnerByStatus, blogsAssistantByStatus);

        foreach (var user in usersList)
        {
            user.EndorsementsReceivedCount = endorsementsReceivedDict.TryGetValue(user.UserId, out var er) ? er : 0;

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

    private async Task PopulatePostReviewCounts(IEnumerable<GeneralUser> users)
    {
        var usersList = users.ToList();
        if (!usersList.Any())
        {
            return;
        }

        var userIds = usersList.Select(u => u.UserId).ToList();

        // Everything the list needs is the first part of everything the profile needs.
        await PopulateListCounts(usersList);

        // Run statistics queries sequentially (DbContext is not thread-safe)
        var givenCounts = await _dmDbContext.PostReviews
            .Where(r => userIds.Contains(r.AuthorId) && !r.IsRemoved)
            .GroupBy(r => r.AuthorId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var receivedCounts = await _dmDbContext.PostReviews
            .Where(r => userIds.Contains(r.PostAuthorId) && !r.IsRemoved)
            .GroupBy(r => r.PostAuthorId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Endorsements (user-to-user recommendations) — two batched
        // COUNT queries, symmetrical to PostReviews above. AuthorId =
        // wrote-by-user, TargetUserId = wrote-about-user.
        var endorsementsGivenCounts = await _dmDbContext.UserEndorsements
            .Where(e => userIds.Contains(e.AuthorId) && !e.IsRemoved)
            .GroupBy(e => e.AuthorId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Game reviews — the same pair of batched COUNTs, over the other kind of
        // review. "Given" is authorship, as everywhere else. "Received" is not:
        // a game review is about a game, so it lands on the game's master, which
        // is the relation GameReviewFilter.GmId already selects by. Both sides
        // have to agree with the endpoints behind the two profile counters, or
        // the number on the profile and the length of the list it links to are
        // answers to different questions.
        var gameReviewsGivenCounts = await _dmDbContext.GameReviews
            .Where(r => userIds.Contains(r.AuthorId) && !r.IsRemoved)
            .GroupBy(r => r.AuthorId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var gameReviewsReceivedCounts = await _dmDbContext.GameReviews
            .Where(r => !r.IsRemoved && !r.Game.IsRemoved && userIds.Contains(r.Game.MasterId))
            .GroupBy(r => r.Game.MasterId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Topics / Comments / GlobalChat counters — drive the achievement
        // metrics TopicsAuthored / CommentsAuthored / GlobalChatMessages.
        // Same batched-GROUP-BY pattern, no per-user N+1.
        var topicsAuthoredCounts = await _dmDbContext.Topics
            .Where(t => userIds.Contains(t.AuthorId) && !t.IsRemoved)
            .GroupBy(t => t.AuthorId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var commentsAuthoredCounts = await _dmDbContext.Comments
            .Where(c => userIds.Contains(c.AuthorId) && !c.IsRemoved)
            .GroupBy(c => c.AuthorId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var globalChatId = Entities.Messaging.Chat.GlobalChatId;
        var globalChatMessageCounts = await _dmDbContext.Messages
            .Where(m => m.ChatId == globalChatId && userIds.Contains(m.UserId) && !m.IsRemoved)
            .GroupBy(m => m.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Bans received — drives the "резиновая уточка" chain. Same
        // batched-GROUP-BY pattern. A ban lifted early does not count: it was taken
        // back, and the count is of bans a user served. Spelled as LiftedUtc == null
        // rather than as the soft-delete flag the ban no longer has; the behaviour
        // is the same as before.
        var bansReceivedCounts = await _dmDbContext.Set<Entities.Moderation.Ban>()
            .Where(b => userIds.Contains(b.TargetUserId) && b.LiftedUtc == null)
            .GroupBy(b => b.TargetUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Game drops — drives the "дропы" chain. The player left voluntarily
        // (IsPlayerLeft=true), the character is Retired, not an NPC; deleted
        // characters and games are excluded. Death and GM exile are not drops.
        var gameDropsCounts = await _dmDbContext.Set<Entities.Game.Characters.Character>()
            .Where(c => c.AuthorId.HasValue
                && userIds.Contains(c.AuthorId.Value)
                && !c.IsNpc
                && !c.IsRemoved
                && !c.Game.IsRemoved
                && c.Status == DM.Domain.Core.Enums.CharacterStatus.Retired
                && c.IsPlayerLeft)
            .GroupBy(c => c.AuthorId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Publications authored — drives the "публикации" chain. Drafts
        // also count (see the enum doc) because filtering is only
        // by IsRemoved.
        var publicationsAuthoredCounts = await _dmDbContext.Set<Entities.Blog.Publication>()
            .Where(p => userIds.Contains(p.AuthorId) && !p.IsRemoved)
            .GroupBy(p => p.AuthorId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Likes received — drives the "лайки" chain. Likes are polymorphic
        // (Like.EntityType + Like.EntityId), so we do 4 separate
        // JOINs with each content type (Topic / Publication / Comment /
        // Message) and sum in .NET. The alternative (one SQL with UNION ALL)
        // would be more complex and less readable.
        // Game posts and PostReviews are not counted — they have their
        // own quality signal "Рейтинг" via PostReview.SignValue.
        var likesOnTopicsCounts = await _dmDbContext.Set<Entities.Shared.Like>()
            .Where(l => !l.IsRemoved && l.EntityType == DM.Domain.Core.Enums.LikeEntityType.Topic)
            .Join(_dmDbContext.Topics.Where(t => !t.IsRemoved && userIds.Contains(t.AuthorId)),
                l => l.EntityId, t => t.TopicId, (l, t) => t.AuthorId)
            .GroupBy(uid => uid)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var likesOnPublicationsCounts = await _dmDbContext.Set<Entities.Shared.Like>()
            .Where(l => !l.IsRemoved && l.EntityType == DM.Domain.Core.Enums.LikeEntityType.Publication)
            .Join(_dmDbContext.Set<Entities.Blog.Publication>().Where(p => !p.IsRemoved && userIds.Contains(p.AuthorId)),
                l => l.EntityId, p => p.PublicationId, (l, p) => p.AuthorId)
            .GroupBy(uid => uid)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var likesOnCommentsCounts = await _dmDbContext.Set<Entities.Shared.Like>()
            .Where(l => !l.IsRemoved && l.EntityType == DM.Domain.Core.Enums.LikeEntityType.Comment)
            .Join(_dmDbContext.Comments.Where(c => !c.IsRemoved && userIds.Contains(c.AuthorId)),
                l => l.EntityId, c => c.CommentId, (l, c) => c.AuthorId)
            .GroupBy(uid => uid)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var likesOnMessagesCounts = await _dmDbContext.Set<Entities.Shared.Like>()
            .Where(l => !l.IsRemoved && l.EntityType == DM.Domain.Core.Enums.LikeEntityType.Message)
            .Join(_dmDbContext.Messages.Where(m => !m.IsRemoved && userIds.Contains(m.UserId)),
                l => l.EntityId, m => m.MessageId, (l, m) => m.UserId)
            .GroupBy(uid => uid)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Subscribers: the total plus a capped preview carrying username, last
        // activity and the settings bitmask the profile UI filters tabs by
        // (games / blogs / topics). Every subscriber row used to be materialised
        // to produce a count and a slice of twenty; the slice is now taken in SQL
        // by ROW_NUMBER() OVER (PARTITION BY TargetId), so the transfer is bounded
        // by the cap instead of by how popular the users on the page are.
        //
        // The Join, rather than relying on the navigation, keeps the count over the
        // same rows the preview comes from: the subscriber's soft-delete filter
        // reaches the join and so removes a departed subscriber from both, which
        // is what the previous single materialised list did.
        var subscriberSummaries = await _dmDbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.User && userIds.Contains(s.TargetId))
            .Join(_dmDbContext.Users, s => s.SubscriberId, u => u.UserId,
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
                    .Select(x => new Domain.Core.Dto.SubscriberInfo
                    {
                        Username = x.Username,
                        LastActivityUtc = x.LastActivityUtc,
                        Settings = x.Settings,
                    })
                    .ToList(),
            })
            .ToDictionaryAsync(x => x.UserId);

        // Username history: fetch and group by user
        var usernameHistoryData = await _dmDbContext.Set<Entities.Account.UsernameHistory>()
            .Where(h => userIds.Contains(h.UserId))
            .OrderByDescending(h => h.ChangedUtc)
            .Select(h => new
            {
                h.UserId,
                Entry = new Domain.Core.Users.UsernameHistoryEntry
                {
                    UsernameHistoryId = h.UsernameHistoryId,
                    OldUsername = h.OldUsername,
                    NewUsername = h.NewUsername,
                    ChangedUtc = h.ChangedUtc,
                    ApprovedByUsername = h.ApprovedBy != null ? h.ApprovedBy.Username : null
                }
            })
            .ToListAsync();

        var usernameHistoryDict = usernameHistoryData
            .GroupBy(h => h.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Entry).ToList());

        var givenDict = givenCounts.ToDictionary(x => x.UserId, x => x.Count);
        var receivedDict = receivedCounts.ToDictionary(x => x.UserId, x => x.Count);
        var endorsementsGivenDict = endorsementsGivenCounts.ToDictionary(x => x.UserId, x => x.Count);
        var gameReviewsGivenDict = gameReviewsGivenCounts.ToDictionary(x => x.UserId, x => x.Count);
        var gameReviewsReceivedDict = gameReviewsReceivedCounts.ToDictionary(x => x.UserId, x => x.Count);
        var topicsAuthoredDict = topicsAuthoredCounts.ToDictionary(x => x.UserId, x => x.Count);
        var commentsAuthoredDict = commentsAuthoredCounts.ToDictionary(x => x.UserId, x => x.Count);
        var globalChatMessagesDict = globalChatMessageCounts.ToDictionary(x => x.UserId, x => x.Count);
        var bansReceivedDict = bansReceivedCounts.ToDictionary(x => x.UserId, x => x.Count);
        var gameDropsDict = gameDropsCounts.ToDictionary(x => x.UserId, x => x.Count);
        var publicationsAuthoredDict = publicationsAuthoredCounts.ToDictionary(x => x.UserId, x => x.Count);

        // Stitch the 4 like sources into one dictionary (UserId → sum).
        var likesReceivedDict = new Dictionary<Guid, int>();
        foreach (var x in likesOnTopicsCounts)
            likesReceivedDict[x.UserId] = (likesReceivedDict.TryGetValue(x.UserId, out var v) ? v : 0) + x.Count;
        foreach (var x in likesOnPublicationsCounts)
            likesReceivedDict[x.UserId] = (likesReceivedDict.TryGetValue(x.UserId, out var v) ? v : 0) + x.Count;
        foreach (var x in likesOnCommentsCounts)
            likesReceivedDict[x.UserId] = (likesReceivedDict.TryGetValue(x.UserId, out var v) ? v : 0) + x.Count;
        foreach (var x in likesOnMessagesCounts)
            likesReceivedDict[x.UserId] = (likesReceivedDict.TryGetValue(x.UserId, out var v) ? v : 0) + x.Count;

        foreach (var user in usersList)
        {
            user.PostReviewsGivenCount = givenDict.TryGetValue(user.UserId, out var given) ? given : 0;
            user.PostReviewsReceivedCount = receivedDict.TryGetValue(user.UserId, out var received) ? received : 0;
            user.EndorsementsGivenCount = endorsementsGivenDict.TryGetValue(user.UserId, out var eg) ? eg : 0;
            user.GameReviewsGivenCount = gameReviewsGivenDict.TryGetValue(user.UserId, out var grg) ? grg : 0;
            user.GameReviewsReceivedCount = gameReviewsReceivedDict.TryGetValue(user.UserId, out var grr) ? grr : 0;
            user.TopicsAuthoredCount = topicsAuthoredDict.TryGetValue(user.UserId, out var ta) ? ta : 0;
            user.CommentsAuthoredCount = commentsAuthoredDict.TryGetValue(user.UserId, out var ca) ? ca : 0;
            user.GlobalChatMessagesCount = globalChatMessagesDict.TryGetValue(user.UserId, out var gc) ? gc : 0;
            user.BansReceivedCount = bansReceivedDict.TryGetValue(user.UserId, out var br) ? br : 0;
            user.GameDropsCount = gameDropsDict.TryGetValue(user.UserId, out var gd) ? gd : 0;
            user.PublicationsAuthoredCount = publicationsAuthoredDict.TryGetValue(user.UserId, out var pa) ? pa : 0;
            user.LikesReceivedCount = likesReceivedDict.TryGetValue(user.UserId, out var lr) ? lr : 0;

            var subscribers = subscriberSummaries.GetValueOrDefault(user.UserId);
            user.Subscribers = subscribers?.Preview ?? [];
            user.SubscribersByCategory = new Domain.Core.Dto.SubscribersByCategory
            {
                Games = subscribers?.GameSubscribers ?? 0,
                Blogs = subscribers?.BlogSubscribers ?? 0,
                Topics = subscribers?.TopicSubscribers ?? 0,
            };
            user.UsernameHistory = usernameHistoryDict.TryGetValue(user.UserId, out var history) ? history : [];
        }
    }
}
