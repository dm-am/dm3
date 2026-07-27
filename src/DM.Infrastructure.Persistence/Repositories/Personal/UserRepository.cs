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

    private static readonly TimeSpan ActivePeriod = TimeSpan.FromDays(30);
    private static readonly TimeSpan OnlinePeriod = TimeSpan.FromMinutes(5);

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
    public Task<int> CountUsersAsync(
        UserActivityFilter filter,
        string? search = null,
        UserRole? role = null,
        bool? isNewbie = null,
        bool? isOnline = null,
        int? minRating = null,
        int? maxRating = null,
        int? minGamesHosting = null,
        int? maxGamesHosting = null,
        int? minGamesPlaying = null,
        int? maxGamesPlaying = null,
        int? minBlogsHosting = null,
        int? maxBlogsHosting = null,
        DateTimeOffset? registeredFromUtc = null,
        DateTimeOffset? registeredToUtc = null) =>
        GetQuery(filter, search, role, isNewbie, isOnline, minRating, maxRating,
            minGamesHosting, maxGamesHosting, minGamesPlaying, maxGamesPlaying, minBlogsHosting, maxBlogsHosting,
            registeredFromUtc, registeredToUtc).CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsersAsync(
        PagingData paging,
        UserActivityFilter filter,
        string? search = null,
        UserRole? role = null,
        UserSort sort = UserSort.Name,
        bool sortAscending = true,
        bool? isNewbie = null,
        bool? isOnline = null,
        int? minRating = null,
        int? maxRating = null,
        int? minGamesHosting = null,
        int? maxGamesHosting = null,
        int? minGamesPlaying = null,
        int? maxGamesPlaying = null,
        int? minBlogsHosting = null,
        int? maxBlogsHosting = null,
        DateTimeOffset? registeredFromUtc = null,
        DateTimeOffset? registeredToUtc = null)
    {
        var baseQuery = GetQuery(filter, search, role, isNewbie, isOnline, minRating, maxRating,
                minGamesHosting, maxGamesHosting, minGamesPlaying, maxGamesPlaying, minBlogsHosting, maxBlogsHosting,
                registeredFromUtc, registeredToUtc)
            .Include(u => u.AvatarUpload)
            .Include(u => u.UsernameHistories);

        IOrderedQueryable<User> orderedQuery;

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
            // 4. Alphabetically within same relevance
            orderedQuery = baseQuery
                .OrderByDescending(u => u.Username.ToLower() == searchLower)
                .ThenByDescending(u => EF.Functions.ILike(u.Username, search + "%"))
                .ThenByDescending(u => EF.Functions.TrigramsSimilarity(u.Username, searchLower))
                .ThenBy(u => u.Username);
        }
        else
        {
            // Explicit sort selected or no search - use specified sort with direction
            // Note: GamesHosting, Popularity, BlogsHosting sorts require subqueries
            orderedQuery = sort switch
            {
                UserSort.Rating => sortAscending
                    ? baseQuery.OrderBy(u => u.RatingDisabled).ThenBy(u => u.QualityRating).ThenBy(u => u.QuantityRating)
                    : baseQuery.OrderBy(u => u.RatingDisabled).ThenByDescending(u => u.QualityRating).ThenByDescending(u => u.QuantityRating),
                // Online users first, then by activity time
                UserSort.LastActivity => sortAscending
                    ? baseQuery.OrderBy(u => u.LastActivityUtc.HasValue && u.LastActivityUtc.Value > _dateTimeProvider.Now - OnlinePeriod)
                        .ThenBy(u => u.LastActivityUtc)
                    : baseQuery.OrderByDescending(u => u.LastActivityUtc.HasValue && u.LastActivityUtc.Value > _dateTimeProvider.Now - OnlinePeriod)
                        .ThenByDescending(u => u.LastActivityUtc),
                UserSort.Registered => sortAscending
                    ? baseQuery.OrderBy(u => u.CreatedUtc)
                    : baseQuery.OrderByDescending(u => u.CreatedUtc),
                UserSort.GamesHosting => sortAscending
                    ? baseQuery.OrderBy(u =>
                        _dmDbContext.Games.Count(g => !g.IsRemoved && g.MasterId == u.UserId) +
                        _dmDbContext.Set<Entities.Game.Links.GameAssistant>().Count(a => a.UserId == u.UserId && !a.Game.IsRemoved)).ThenBy(u => u.Username)
                    : baseQuery.OrderByDescending(u =>
                        _dmDbContext.Games.Count(g => !g.IsRemoved && g.MasterId == u.UserId) +
                        _dmDbContext.Set<Entities.Game.Links.GameAssistant>().Count(a => a.UserId == u.UserId && !a.Game.IsRemoved)).ThenBy(u => u.Username),
                UserSort.BlogsHosting => sortAscending
                    ? baseQuery.OrderBy(u =>
                        _dmDbContext.Blogs.Count(b => !b.IsRemoved && b.AuthorId == u.UserId) +
                        _dmDbContext.Set<Entities.Blog.BlogAssistant>().Count(a => a.UserId == u.UserId && !a.Blog.IsRemoved)).ThenBy(u => u.Username)
                    : baseQuery.OrderByDescending(u =>
                        _dmDbContext.Blogs.Count(b => !b.IsRemoved && b.AuthorId == u.UserId) +
                        _dmDbContext.Set<Entities.Blog.BlogAssistant>().Count(a => a.UserId == u.UserId && !a.Blog.IsRemoved)).ThenBy(u => u.Username),
                UserSort.Popularity => sortAscending
                    ? baseQuery.OrderBy(u =>
                        _dmDbContext.Subscriptions.Count(s => s.TargetType == SubscriptionTargetType.User && s.TargetId == u.UserId &&
                            s.Subscriber.LastActivityUtc.HasValue && s.Subscriber.LastActivityUtc.Value > _dateTimeProvider.Now - ActivePeriod)).ThenBy(u => u.Username)
                    : baseQuery.OrderByDescending(u =>
                        _dmDbContext.Subscriptions.Count(s => s.TargetType == SubscriptionTargetType.User && s.TargetId == u.UserId &&
                            s.Subscriber.LastActivityUtc.HasValue && s.Subscriber.LastActivityUtc.Value > _dateTimeProvider.Now - ActivePeriod)).ThenBy(u => u.Username),
                // Count distinct games where user has at least one character (current or former player)
                UserSort.GamesPlaying => sortAscending
                    ? baseQuery.OrderBy(u =>
                        _dmDbContext.Set<Entities.Game.Characters.Character>()
                            .Where(c => c.AuthorId == u.UserId && !c.IsNpc && !c.IsRemoved && !c.Game.IsRemoved)
                            .Select(c => c.GameId).Distinct().Count()).ThenBy(u => u.Username)
                    : baseQuery.OrderByDescending(u =>
                        _dmDbContext.Set<Entities.Game.Characters.Character>()
                            .Where(c => c.AuthorId == u.UserId && !c.IsNpc && !c.IsRemoved && !c.Game.IsRemoved)
                            .Select(c => c.GameId).Distinct().Count()).ThenBy(u => u.Username),
                _ => sortAscending
                    ? baseQuery.OrderBy(u => u.Username)
                    : baseQuery.OrderByDescending(u => u.Username)
            };
        }

        var users = await orderedQuery
            .Page(paging)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        await PopulatePostReviewCounts(users);
        return users;
    }

    /// <inheritdoc />
    public async Task<GeneralUser?> GetUserAsync(string username)
    {
        var user = await _dmDbContext.Users
            .Include(u => u.AvatarUpload)
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
    public async Task<GeneralUser?> GetUserAsync(Guid userId)
    {
        var user = await _dmDbContext.Users
            .Include(u => u.AvatarUpload)
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
            .Include(u => u.AvatarUpload)
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
            .Include(u => u.AvatarUpload)
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
            .Include(u => u.AvatarUpload)
            .Where(u => !u.IsRemoved && EF.Functions.ILike(u.Email, email))
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
            .Include(u => u.AvatarUpload)
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
            .Include(u => u.AvatarUpload)
            .Where(u => !u.IsRemoved && idsList.Contains(u.UserId))
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        await PopulatePostReviewCounts(users);
        return users;
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

        if (existingSettings == null)
        {
            // Create new settings document if it doesn't exist
            var newSettings = new UserSettings
            {
                UserId = settingsUpdate.UserId,
                Theme = settingsUpdate.Theme?.Value ?? DM.Domain.Core.Enums.Theme.Light,
                Paging = new PagingSettings
                {
                    CommentsPerPage = settingsUpdate.CommentsPerPage?.Value ?? 20,
                    TopicsPerPage = settingsUpdate.TopicsPerPage?.Value ?? 20,
                    MessagesPerPage = settingsUpdate.MessagesPerPage?.Value ?? 20,
                    PostsPerPage = settingsUpdate.PostsPerPage?.Value ?? 20,
                    EntitiesPerPage = settingsUpdate.EntitiesPerPage?.Value ?? 10
                }
            };
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
            upload.DeletedUtc = DateTimeOffset.UtcNow;
        }

        await _dmDbContext.SaveChangesAsync();
    }

    // ═══ PRIVATE ═══

    private const int NewbieThreshold = 100;

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

    private IQueryable<User> GetQuery(
        UserActivityFilter filter,
        string? search = null,
        UserRole? role = null,
        bool? isNewbie = null,
        bool? isOnline = null,
        int? minRating = null,
        int? maxRating = null,
        int? minGamesHosting = null,
        int? maxGamesHosting = null,
        int? minGamesPlaying = null,
        int? maxGamesPlaying = null,
        int? minBlogsHosting = null,
        int? maxBlogsHosting = null,
        DateTimeOffset? registeredFromUtc = null,
        DateTimeOffset? registeredToUtc = null)
    {
        IQueryable<User> query = filter switch
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
        var activeRange = now - ActivePeriod;
        var onlineRange = now - OnlinePeriod;

        if (filter == UserActivityFilter.Active)
        {
            query = query.Where(u => u.LastActivityUtc.HasValue && u.LastActivityUtc > activeRange);
        }
        else if (filter == UserActivityFilter.Inactive)
        {
            query = query.Where(u => !u.LastActivityUtc.HasValue || u.LastActivityUtc <= activeRange);
        }

        // Online filter: users active in last 5 minutes
        if (isOnline == true)
        {
            query = query.Where(u => u.LastActivityUtc.HasValue && u.LastActivityUtc > onlineRange);
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        // Newbie filter (users with < 100 posts)
        if (isNewbie.HasValue)
        {
            if (isNewbie.Value)
            {
                query = query.Where(u => u.QuantityRating < NewbieThreshold);
            }
            else
            {
                query = query.Where(u => u.QuantityRating >= NewbieThreshold);
            }
        }

        // Rating filter (based on QualityRating = post review score sum)
        if (minRating.HasValue)
        {
            query = query.Where(u => !u.RatingDisabled && u.QualityRating >= minRating.Value);
        }
        if (maxRating.HasValue)
        {
            query = query.Where(u => !u.RatingDisabled && u.QualityRating <= maxRating.Value);
        }

        // Registration date range filter
        if (registeredFromUtc.HasValue)
        {
            query = query.Where(u => u.CreatedUtc >= registeredFromUtc.Value);
        }
        if (registeredToUtc.HasValue)
        {
            query = query.WhereAtOrBefore(u => u.CreatedUtc, registeredToUtc.Value);
        }

        // Games hosting filter (master + assistant)
        if (minGamesHosting.HasValue)
        {
            query = query.Where(u =>
                _dmDbContext.Games.Count(g => !g.IsRemoved && g.MasterId == u.UserId) +
                _dmDbContext.Set<Entities.Game.Links.GameAssistant>().Count(a => a.UserId == u.UserId && !a.Game.IsRemoved)
                >= minGamesHosting.Value);
        }
        if (maxGamesHosting.HasValue)
        {
            query = query.Where(u =>
                _dmDbContext.Games.Count(g => !g.IsRemoved && g.MasterId == u.UserId) +
                _dmDbContext.Set<Entities.Game.Links.GameAssistant>().Count(a => a.UserId == u.UserId && !a.Game.IsRemoved)
                <= maxGamesHosting.Value);
        }

        // Games playing filter (distinct games where user has active character)
        if (minGamesPlaying.HasValue)
        {
            query = query.Where(u =>
                _dmDbContext.Set<Entities.Game.Characters.Character>()
                    .Where(c => c.AuthorId == u.UserId && !c.IsNpc && !c.IsRemoved && !c.Game.IsRemoved)
                    .Select(c => c.GameId).Distinct().Count()
                >= minGamesPlaying.Value);
        }
        if (maxGamesPlaying.HasValue)
        {
            query = query.Where(u =>
                _dmDbContext.Set<Entities.Game.Characters.Character>()
                    .Where(c => c.AuthorId == u.UserId && !c.IsNpc && !c.IsRemoved && !c.Game.IsRemoved)
                    .Select(c => c.GameId).Distinct().Count()
                <= maxGamesPlaying.Value);
        }

        // Blogs hosting filter (owner + assistant)
        if (minBlogsHosting.HasValue)
        {
            query = query.Where(u =>
                _dmDbContext.Blogs.Count(b => !b.IsRemoved && b.AuthorId == u.UserId) +
                _dmDbContext.Set<Entities.Blog.BlogAssistant>().Count(a => a.UserId == u.UserId && !a.Blog.IsRemoved)
                >= minBlogsHosting.Value);
        }
        if (maxBlogsHosting.HasValue)
        {
            query = query.Where(u =>
                _dmDbContext.Blogs.Count(b => !b.IsRemoved && b.AuthorId == u.UserId) +
                _dmDbContext.Set<Entities.Blog.BlogAssistant>().Count(a => a.UserId == u.UserId && !a.Blog.IsRemoved)
                <= maxBlogsHosting.Value);
        }

        return query;
    }

    private async Task PopulatePostReviewCounts(IEnumerable<GeneralUser> users)
    {
        var usersList = users.ToList();
        if (!usersList.Any())
        {
            return;
        }

        var userIds = usersList.Select(u => u.UserId).ToList();

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

        var endorsementsReceivedCounts = await _dmDbContext.UserEndorsements
            .Where(e => userIds.Contains(e.TargetUserId) && !e.IsRemoved)
            .GroupBy(e => e.TargetUserId)
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
        // batched-GROUP-BY pattern. Soft-deleted bans excluded (IsRemoved):
        // tidying ban history shouldn't retroactively erase the achievement,
        // but if a ban gets revoked entirely we don't want to keep counting it.
        var bansReceivedCounts = await _dmDbContext.Set<Entities.Moderation.Ban>()
            .Where(b => userIds.Contains(b.TargetUserId) && !b.IsRemoved)
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

        // Subscribers: fetch username + last activity + settings flags
        // for richer profile display. Settings is the bitmask the profile
        // UI uses to filter subscribers per active tab (games / blogs /
        // topics). The username-only list is kept for tooltip compatibility.
        // Limit retained at 20 per user — same budget across all three fields.
        var subscriberData = await _dmDbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.User && userIds.Contains(s.TargetId))
            .Select(s => new
            {
                s.TargetId,
                Username = s.Subscriber.Username,
                LastActivityUtc = s.Subscriber.LastActivityUtc,
                Settings = s.Settings,
            })
            .ToListAsync();

        var subscribersDict = subscriberData
            .GroupBy(s => s.TargetId)
            .ToDictionary(g => g.Key, g => g.Count());

        var subscriberUsernamesDict = subscriberData
            .GroupBy(s => s.TargetId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => s.Username).Take(20).ToList());

        var subscribersInfoDict = subscriberData
            .GroupBy(s => s.TargetId)
            .ToDictionary(
                g => g.Key,
                g => g.Take(20)
                    .Select(s => new Domain.Core.Dto.SubscriberInfo
                    {
                        Username = s.Username,
                        LastActivityUtc = s.LastActivityUtc,
                        Settings = s.Settings,
                    })
                    .ToList());

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
        var endorsementsReceivedDict = endorsementsReceivedCounts.ToDictionary(x => x.UserId, x => x.Count);
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

        // Build status breakdown dictionaries
        var gamesHostingByStatusDict = BuildStatusBreakdownDict(gamesMasterByStatus, gamesAssistantByStatus);
        var gamesPlayingByStatusDict = BuildStatusBreakdownDict(gamesPlayingByStatus, null);
        var blogsHostingByStatusDict = BuildStatusBreakdownDict(blogsOwnerByStatus, blogsAssistantByStatus);

        foreach (var user in usersList)
        {
            user.PostReviewsGivenCount = givenDict.TryGetValue(user.UserId, out var given) ? given : 0;
            user.PostReviewsReceivedCount = receivedDict.TryGetValue(user.UserId, out var received) ? received : 0;
            user.EndorsementsGivenCount = endorsementsGivenDict.TryGetValue(user.UserId, out var eg) ? eg : 0;
            user.EndorsementsReceivedCount = endorsementsReceivedDict.TryGetValue(user.UserId, out var er) ? er : 0;
            user.TopicsAuthoredCount = topicsAuthoredDict.TryGetValue(user.UserId, out var ta) ? ta : 0;
            user.CommentsAuthoredCount = commentsAuthoredDict.TryGetValue(user.UserId, out var ca) ? ca : 0;
            user.GlobalChatMessagesCount = globalChatMessagesDict.TryGetValue(user.UserId, out var gc) ? gc : 0;
            user.BansReceivedCount = bansReceivedDict.TryGetValue(user.UserId, out var br) ? br : 0;
            user.GameDropsCount = gameDropsDict.TryGetValue(user.UserId, out var gd) ? gd : 0;
            user.PublicationsAuthoredCount = publicationsAuthoredDict.TryGetValue(user.UserId, out var pa) ? pa : 0;
            user.LikesReceivedCount = likesReceivedDict.TryGetValue(user.UserId, out var lr) ? lr : 0;

            // Games hosting = sum from status breakdown
            user.GamesHostingByStatus = gamesHostingByStatusDict.TryGetValue(user.UserId, out var gh) ? gh : null;
            user.GamesHosting = user.GamesHostingByStatus?.Total ?? 0;

            // Games playing = sum from status breakdown
            user.GamesPlayingByStatus = gamesPlayingByStatusDict.TryGetValue(user.UserId, out var gp) ? gp : null;
            user.GamesPlaying = user.GamesPlayingByStatus?.Total ?? 0;

            // Blogs hosting = sum from status breakdown
            user.BlogsHostingByStatus = blogsHostingByStatusDict.TryGetValue(user.UserId, out var bh) ? bh : null;
            user.BlogsHosting = user.BlogsHostingByStatus?.Total ?? 0;

            user.SubscribersCount = subscribersDict.TryGetValue(user.UserId, out var subs) ? subs : 0;
            user.SubscriberUsernames = subscriberUsernamesDict.TryGetValue(user.UserId, out var names) ? names : [];
            user.Subscribers = subscribersInfoDict.TryGetValue(user.UserId, out var infos) ? infos : [];
            user.UsernameHistory = usernameHistoryDict.TryGetValue(user.UserId, out var history) ? history : [];
        }
    }
}
