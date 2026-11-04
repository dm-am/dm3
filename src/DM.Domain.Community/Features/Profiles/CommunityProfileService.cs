using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;

namespace DM.Domain.Community.Features.Profiles;

/// <inheritdoc />
internal class CommunityProfileService : ICommunityProfileService
{
    private readonly IUserReadRepository _userRepository;
    private readonly IUsernameHistoryReader _usernameHistoryReader;
    private readonly IIdentityProvider _identityProvider;
    private readonly ICache _cache;

    public CommunityProfileService(
        IUserReadRepository userRepository,
        IUsernameHistoryReader usernameHistoryReader,
        IIdentityProvider identityProvider,
        ICache cache)
    {
        _userRepository = userRepository;
        _usernameHistoryReader = usernameHistoryReader;
        _identityProvider = identityProvider;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetProfile(string username) =>
        await _cache.GetOrCreateAsync(
            CacheKeys.UserDetails(username),
            () => _userRepository.GetUserDetailsAsync(username),
            CachePolicy.Medium)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));

    /// <inheritdoc />
    public async Task<UserDetails> GetProfile(Guid userId) =>
        await _cache.GetOrCreateAsync(
            CacheKeys.UserDetails(userId),
            () => _userRepository.GetUserDetailsAsync(userId),
            CachePolicy.Medium)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundById(userId));

    /// <inheritdoc />
    public async Task<(IEnumerable<GeneralUser> users, PagingResult paging)> GetUsers(
        PagingQuery query, UserFilter filter)
    {
        // One filter instance for both reads, so the total and the page cannot
        // disagree about what was filtered.
        var totalCount = await _userRepository.CountUsersAsync(filter);
        var paging = new PagingData(query, _identityProvider.Current.Settings.PageSize, totalCount);
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

    /// <summary>
    /// Key of the one cached summary. It depends on nobody's identity - the sums are
    /// of public counters, and every reader is shown the same ones - so one entry
    /// serves the whole site.
    /// </summary>
    private const string RemovedAccountSummaryKey = "removed_account_summary";

    /// <inheritdoc />
    /// <remarks>
    /// Cached for the short time the lists are: the sums are counted over every
    /// removed account on the site, and a minute is well below what anybody could
    /// tell from them.
    /// </remarks>
    public Task<GeneralUser> GetRemovedAccountSummary() =>
        _cache.GetOrCreateAsync(
            RemovedAccountSummaryKey,
            () => _userRepository.GetRemovedAccountSummaryAsync(),
            CachePolicy.Medium);
}
