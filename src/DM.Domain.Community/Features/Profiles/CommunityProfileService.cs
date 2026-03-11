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
            $"user_details_{normalizedUsername}",
            () => _userRepository.GetUserDetailsAsync(username),
            CachePolicy.Medium);

        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"Пользователь {username} не найден");
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetProfile(Guid userId)
    {
        var user = await _cache.GetOrCreateAsync(
            $"user_details_{userId}",
            () => _userRepository.GetUserDetailsAsync(userId),
            CachePolicy.Medium);

        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"Пользователь с ID {userId} не найден");
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<GeneralUser> users, PagingResult paging)> GetUsers(
        PagingQuery query,
        UserActivityFilter filter,
        string? search = null,
        UserRole? role = null,
        UserSort sort = UserSort.Name)
    {
        if (filter == UserActivityFilter.Pending)
        {
            _intentionManager.ThrowIfForbidden(CommunityIntention.ViewPendingUsers);
        }

        var totalCount = await _userRepository.CountUsersAsync(filter, search, role);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);
        var users = await _userRepository.GetUsersAsync(paging, filter, search, role, sort);
        return (users, paging.Result);
    }

    /// <inheritdoc />
    public Task<IEnumerable<GeneralUser>> GetUsersByRole(UserRole role) =>
        _cache.GetOrCreateAsync(
            $"users_by_role_{role}",
            () => _userRepository.GetUsersByRoleAsync(role),
            CachePolicy.LongLived);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<UsernameHistoryEntry>> GetUsernameHistory(Guid userId) =>
        _usernameHistoryReader.GetByUserIdAsync(userId);
}
