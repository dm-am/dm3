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
            $"user_details_{normalizedUsername}",
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

        // Invalidate cache
        await _cache.InvalidateAsync($"user_details_{username.ToLowerInvariant()}");

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

        await _moderatedProfileRepository.SetUserRole(username, role);

        // Invalidate cache
        await _cache.InvalidateAsync($"user_details_{username.ToLowerInvariant()}");
    }
}
