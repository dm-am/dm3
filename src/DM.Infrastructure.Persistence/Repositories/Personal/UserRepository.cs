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
    public Task<int> CountUsers(UserActivityFilter filter, string? search = null, UserRole? role = null) =>
        GetQuery(filter, search, role).CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsers(
        PagingData paging,
        UserActivityFilter filter,
        string? search = null,
        UserRole? role = null,
        UserSort sort = UserSort.Name)
    {
        var baseQuery = GetQuery(filter, search, role)
            .Include(u => u.AvatarUpload);

        IOrderedQueryable<User> orderedQuery = sort switch
        {
            UserSort.Rating => baseQuery
                .OrderBy(u => u.RatingDisabled)
                .ThenByDescending(u => u.QualityRating)
                .ThenByDescending(u => u.QuantityRating),
            _ => baseQuery
                .OrderBy(u => u.Username)
        };

        var users = await orderedQuery
            .Page(paging)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        await PopulatePostReviewCounts(users);
        return users;
    }

    /// <inheritdoc />
    public async Task<GeneralUser?> GetUser(string username)
    {
        var user = await _dmDbContext.Users
            .Include(u => u.AvatarUpload)
            .Where(u => !u.IsRemoved && EF.Functions.ILike(u.Username, username))
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (user != null)
        {
            await PopulatePostReviewCounts(new[] { user });
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<GeneralUser?> GetUser(Guid userId)
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
    public async Task<UserDetails?> GetUserDetails(string username)
    {
        var userDetails = await _dmDbContext.Users
            .Include(u => u.AvatarUpload)
            .Where(u => !u.IsRemoved && EF.Functions.ILike(u.Username, username))
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
    public async Task<UserDetails?> GetUserDetails(Guid userId)
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
    public async Task<IEnumerable<GeneralUser>> GetUsersByRole(UserRole role)
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
        _dmDbContext.Reviews
            .Where(r => r.UserId == userId && r.TargetType == ReviewTargetType.Post)
            .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsers(IEnumerable<Guid> userIds)
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

    // ═══ PRIVATE ═══

    private IQueryable<User> GetQuery(UserActivityFilter filter, string? search = null, UserRole? role = null)
    {
        IQueryable<User> query = filter switch
        {
            UserActivityFilter.Pending => _dmDbContext.Users.Where(u => false),
            _ => _dmDbContext.Users.Where(u => !u.IsRemoved)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = search.Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(u => EF.Functions.ILike(u.Username, searchPattern));
        }

        if (filter == UserActivityFilter.Active)
        {
            var activeRange = _dateTimeProvider.Now - ActivePeriod;
            query = query.Where(u => u.LastActivityUtc.HasValue && u.LastActivityUtc > activeRange);
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
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

        var givenCounts = await _dmDbContext.Reviews
            .Where(r => userIds.Contains(r.UserId) && r.TargetType == ReviewTargetType.Post && !r.IsRemoved)
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var receivedCounts = await _dmDbContext.Reviews
            .Where(r => r.PostAuthorId.HasValue && userIds.Contains(r.PostAuthorId.Value) && r.TargetType == ReviewTargetType.Post && !r.IsRemoved)
            .GroupBy(r => r.PostAuthorId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var givenDict = givenCounts.ToDictionary(x => x.UserId, x => x.Count);
        var receivedDict = receivedCounts.ToDictionary(x => x.UserId, x => x.Count);

        foreach (var user in usersList)
        {
            user.PostReviewsGivenCount = givenDict.TryGetValue(user.UserId, out var given) ? given : 0;
            user.PostReviewsReceivedCount = receivedDict.TryGetValue(user.UserId, out var received) ? received : 0;
        }
    }
}
