using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Authorization;

namespace DM.Domain.Moderation.Features.Profiles;

/// <summary>
/// Service implementation for moderated user profiles.
/// </summary>
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
    public async Task<UserDetails> ModerateProfile(string username, string info)
    {
        var user = await _userRepository.GetUserAsync(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));
        }

        _intentionManager.ThrowIfForbidden(ModerationIntention.ModerateUserProfile);

        await _moderatedProfileRepository.UpdateUserInfo(username, info);

        // Both keys of the same document: it is read by name on the profile page and
        // by identifier everywhere a link to that person is built.
        await _cache.InvalidateAsync(CacheKeys.UserDetails(username));
        await _cache.InvalidateAsync(CacheKeys.UserDetails(user.UserId));

        return await GetProfile(username);
    }

    /// <inheritdoc />
    public async Task<UserDetails> SetModerationWatch(string username, bool underWatch)
    {
        var user = await _userRepository.GetUserAsync(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));
        }

        _intentionManager.ThrowIfForbidden(ModerationIntention.SetModerationWatch);

        await _moderatedProfileRepository.SetModerationWatch(username, underWatch);

        // Both keys of the same document, as everywhere else here: the profile is
        // read by name on the page and by identifier from every link to it, and a
        // stale copy of this particular field decides whether the user's next game
        // is premoderated.
        await _cache.InvalidateAsync(CacheKeys.UserDetails(username));
        await _cache.InvalidateAsync(CacheKeys.UserDetails(user.UserId));

        return await GetProfile(username);
    }

    /// <inheritdoc />
    public async Task SetUserRole(string username, UserRole role)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.SetUserRole);

        if (role == UserRole.Guest)
        {
            throw new HttpBadRequestException(
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["role"] = "Нельзя назначить роль \"Гость\""
                });
        }

        // Read before the write, for the identifier and for the role being left.
        // The repository throws on an unknown name, which the pipeline turns into a
        // 500 on an endpoint that documents a 404.
        var user = await _userRepository.GetUserAsync(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));
        }

        var previousRole = user.Role;

        await _moderatedProfileRepository.SetUserRole(username, role);

        // Four keys, because a role change moves the user between two lists and is
        // read back by two different keys. The role listing lives an hour and was
        // invalidated by nothing at all: for that hour the staff page showed the
        // person under the role they no longer hold, and the role they now hold
        // showed one name short.
        await _cache.InvalidateAsync(CacheKeys.UserDetails(username));
        await _cache.InvalidateAsync(CacheKeys.UserDetails(user.UserId));
        await _cache.InvalidateAsync(CacheKeys.UsersByRole(previousRole));
        await _cache.InvalidateAsync(CacheKeys.UsersByRole(role));
    }
}
