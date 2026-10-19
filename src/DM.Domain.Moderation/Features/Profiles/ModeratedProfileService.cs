using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Authorization;

namespace DM.Domain.Moderation.Features.Profiles;

/// <inheritdoc />
internal class ModeratedProfileService : IModeratedProfileService
{
    private readonly IUserReadRepository _userRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly ICache _cache;
    private readonly IModeratedProfileRepository _moderatedProfileRepository;

    public ModeratedProfileService(
        IUserReadRepository userRepository,
        IIntentionManager intentionManager,
        ICache cache,
        IModeratedProfileRepository moderatedProfileRepository)
    {
        _userRepository = userRepository;
        _intentionManager = intentionManager;
        _cache = cache;
        _moderatedProfileRepository = moderatedProfileRepository;
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetProfile(string username) =>
        await _cache.GetOrCreateAsync(
            CacheKeys.UserDetails(username),
            () => _userRepository.GetUserDetailsAsync(username),
            CachePolicy.Medium)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));

    /// <inheritdoc />
    public async Task<UserDetails> ModerateProfile(string username, string info)
    {
        var user = await GetUser(username);
        _intentionManager.ThrowIfForbidden(ModerationIntention.ModerateUserProfile);

        await _moderatedProfileRepository.UpdateUserInfo(username, info);
        await InvalidateUserDetails(username, user.UserId);

        return await GetProfile(username);
    }

    /// <inheritdoc />
    public async Task<UserDetails> SetModerationWatch(string username, bool underWatch)
    {
        var user = await GetUser(username);
        _intentionManager.ThrowIfForbidden(ModerationIntention.SetModerationWatch);

        await _moderatedProfileRepository.SetModerationWatch(username, underWatch);

        // A stale copy of this field decides whether the user's next game is premoderated.
        await InvalidateUserDetails(username, user.UserId);

        return await GetProfile(username);
    }

    /// <inheritdoc />
    public async Task SetUserRole(string username, UserRole role)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.SetUserRole);

        if (role == UserRole.Guest)
        {
            throw new HttpBadRequestException(
                new Dictionary<string, string>
                {
                    ["role"] = "Нельзя назначить роль \"Гость\""
                });
        }

        // Read before the write, for the role being left and for the 404: the repository
        // throws on an unknown name.
        var user = await GetUser(username);
        var previousRole = user.Role;

        await _moderatedProfileRepository.SetUserRole(username, role);

        // The role change also moves the user between two role listings.
        await InvalidateUserDetails(username, user.UserId);
        await _cache.InvalidateAsync(CacheKeys.UsersByRole(previousRole));
        await _cache.InvalidateAsync(CacheKeys.UsersByRole(role));
    }

    private async Task<GeneralUser> GetUser(string username) =>
        await _userRepository.GetUserAsync(username)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));

    /// <summary>
    /// Both keys of one document: the profile is read by name on its page and by
    /// identifier from every link to it.
    /// </summary>
    private async Task InvalidateUserDetails(string username, Guid userId)
    {
        await _cache.InvalidateAsync(CacheKeys.UserDetails(username));
        await _cache.InvalidateAsync(CacheKeys.UserDetails(userId));
    }
}
