using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Community.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;

namespace DM.Domain.Community.Features.Profiles;

/// <summary>
/// Service implementation for public user profiles.
/// </summary>
internal class CommunityProfileService : ICommunityProfileService
{
    private readonly IUserReadRepository _userRepository;
    private readonly IUsernameHistoryReader _usernameHistoryReader;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ICache _cache;

    public CommunityProfileService(
        IUserReadRepository userRepository,
        IUsernameHistoryReader usernameHistoryReader,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ICache cache)
    {
        _userRepository = userRepository;
        _usernameHistoryReader = usernameHistoryReader;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetProfile(string username)
    {
        var normalizedUsername = username.ToLowerInvariant();
        var user = await _cache.GetOrCreateAsync(
            CacheKeys.UserDetails(normalizedUsername),
            () => _userRepository.GetUserDetailsAsync(username),
            CachePolicy.Medium);

        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetProfile(Guid userId)
    {
        var user = await _cache.GetOrCreateAsync(
            CacheKeys.UserDetails(userId),
            () => _userRepository.GetUserDetailsAsync(userId),
            CachePolicy.Medium);

        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundById(userId));
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<GeneralUser> users, PagingResult paging)> GetUsers(
        PagingQuery query, UserFilter filter)
    {
        if (filter.Activity == UserActivityFilter.Pending)
        {
            _intentionManager.ThrowIfForbidden(CommunityIntention.ViewPendingUsers);
        }

        // One filter instance for both reads, so the total and the page cannot
        // disagree about what was filtered.
        var totalCount = await _userRepository.CountUsersAsync(filter);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);
        var users = await _userRepository.GetUsersAsync(paging, filter);
        return (users, paging.Result);
    }

    /// <inheritdoc />
    public Task<IEnumerable<GeneralUser>> GetUsersByRole(UserRole role) =>
        _cache.GetOrCreateAsync(
            CacheKeys.UsersByRole(role),
            () => _userRepository.GetUsersByRoleAsync(role),
            CachePolicy.LongLived);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<UsernameHistoryEntry>> GetUsernameHistory(Guid userId) =>
        _usernameHistoryReader.GetByUserIdAsync(userId);
}
