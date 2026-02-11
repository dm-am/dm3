using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Extensions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.BusinessObjects.Users.Settings;
using DM.Services.DataAccess.MongoIntegration;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace DM.Services.Community.BusinessProcesses.Users.Reading;

/// <inheritdoc />
internal class UserReadingRepository : MongoCollectionRepository<UserSettings>, IUserReadingRepository
{
    private readonly DmDbContext _dmDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserReadingRepository(
        DmDbContext dmDbContext,
        DmMongoClient mongoClient,
        IDateTimeProvider dateTimeProvider,
        IMapper mapper) : base(mongoClient)
    {
        _dmDbContext = dmDbContext;
        _dateTimeProvider = dateTimeProvider;
        _mapper = mapper;
    }

    private static readonly TimeSpan ActivePeriod = TimeSpan.FromDays(30);

    /// <inheritdoc />
    public Task<int> CountUsers(UserActivityFilter filter, string? search = null) =>
        GetQuery(filter, search).CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsers(PagingData paging, UserActivityFilter filter, string? search = null)
    {
        var users = await GetQuery(filter, search)
            .Include(u => u.AvatarUpload)
            .OrderBy(u => u.RatingDisabled)
            .ThenByDescending(u => u.QualityRating)
            .ThenBy(u => u.QuantityRating)
            .Page(paging)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        await PopulatePostReviewsGivenCount(users);
        return users;
    }

    private IQueryable<User> GetQuery(UserActivityFilter filter, string? search = null)
    {
        // Note: Pending filter returns empty query here - PendingRegistrations are queried separately
        IQueryable<User> query = filter switch
        {
            UserActivityFilter.Pending => _dmDbContext.Users.Where(u => false), // PendingRegistrations are handled separately
            _ => _dmDbContext.Users.Where(u => !u.IsRemoved)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = search.Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(u => EF.Functions.ILike(u.Login, searchPattern));
        }

        if (filter == UserActivityFilter.Active)
        {
            var activeRange = _dateTimeProvider.Now - ActivePeriod;
            query = query.Where(u => u.LastActivityUtc.HasValue && u.LastActivityUtc > activeRange);
        }

        return query;
    }

    /// <inheritdoc />
    public async Task<GeneralUser?> GetUser(string login)
    {
        var user = await _dmDbContext.Users
            .Include(u => u.AvatarUpload)
            .Where(u => !u.IsRemoved && EF.Functions.ILike(u.Login, login))
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (user != null)
        {
            await PopulatePostReviewsGivenCount(new[] { user });
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
            await PopulatePostReviewsGivenCount(new[] { user });
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<UserDetails?> GetUserDetails(string login)
    {
        var userDetails = await _dmDbContext.Users
            .Include(u => u.AvatarUpload)
            .Where(u => !u.IsRemoved && EF.Functions.ILike(u.Login, login))
            .ProjectTo<UserDetails>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (userDetails == null)
        {
            return null;
        }

        await PopulatePostReviewsGivenCount(new[] { userDetails });

        var userSettings = await Collection
            .Find(Filter.Eq(u => u.UserId, userDetails.UserId))
            .FirstOrDefaultAsync();
        userDetails.Settings = userSettings == null
            ? Authentication.Dto.UserSettings.Default
            : _mapper.Map<Authentication.Dto.UserSettings>(userSettings);
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

        await PopulatePostReviewsGivenCount(new[] { userDetails });

        var userSettings = await Collection
            .Find(Filter.Eq(u => u.UserId, userDetails.UserId))
            .FirstOrDefaultAsync();
        userDetails.Settings = userSettings == null
            ? Authentication.Dto.UserSettings.Default
            : _mapper.Map<Authentication.Dto.UserSettings>(userSettings);
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

        await PopulatePostReviewsGivenCount(new[] { userDetails });

        var userSettings = await Collection
            .Find(Filter.Eq(u => u.UserId, userDetails.UserId))
            .FirstOrDefaultAsync();
        userDetails.Settings = userSettings == null
            ? Authentication.Dto.UserSettings.Default
            : _mapper.Map<Authentication.Dto.UserSettings>(userSettings);
        return userDetails;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsersByRole(UserRole role)
    {
        var users = await _dmDbContext.Users
            .Include(u => u.AvatarUpload)
            .Where(u => !u.IsRemoved && u.Role == role)
            .OrderBy(u => u.Login)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        await PopulatePostReviewsGivenCount(users);
        return users;
    }

    /// <inheritdoc />
    public Task<int> GetPostReviewsGivenCount(Guid userId) =>
        _dmDbContext.Reviews
            .Where(r => r.UserId == userId && r.TargetType == Core.Dto.Enums.ReviewTargetType.Post)
            .CountAsync();

    private async Task PopulatePostReviewsGivenCount(IEnumerable<GeneralUser> users)
    {
        var usersList = users.ToList();
        if (!usersList.Any())
        {
            return;
        }

        var userIds = usersList.Select(u => u.UserId).ToList();
        var reviewCounts = await _dmDbContext.Reviews
            .Where(r => userIds.Contains(r.UserId) && r.TargetType == Core.Dto.Enums.ReviewTargetType.Post && !r.IsRemoved)
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countDictionary = reviewCounts.ToDictionary(x => x.UserId, x => x.Count);
        foreach (var user in usersList)
        {
            user.PostReviewsGivenCount = countDictionary.TryGetValue(user.UserId, out var count) ? count : 0;
        }
    }
}