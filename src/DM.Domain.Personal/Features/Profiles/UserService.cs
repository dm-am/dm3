using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Authorization;
using FluentValidation;

namespace DM.Domain.Personal.Features.Profiles;

/// <inheritdoc />
internal class UserService : IUserService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserRepository _repository;
    private readonly IUsernameHistoryReader _usernameHistoryReader;
    private readonly IIntentionManager _intentionManager;
    private readonly ICache _cache;
    private readonly IValidator<UpdateUser> _validator;
    private readonly IUploadGarbageCollector _uploadsCleanup;
    private readonly IGuidFactory _guidFactory;
    private readonly IRealtimeAvatarBroadcaster _avatarBroadcaster;

    /// <inheritdoc />
    public UserService(
        IIdentityProvider identityProvider,
        IUserRepository repository,
        IUsernameHistoryReader usernameHistoryReader,
        IIntentionManager intentionManager,
        ICache cache,
        IValidator<UpdateUser> validator,
        IUploadGarbageCollector uploadsCleanup,
        IGuidFactory guidFactory,
        IRealtimeAvatarBroadcaster avatarBroadcaster)
    {
        _identityProvider = identityProvider;
        _repository = repository;
        _usernameHistoryReader = usernameHistoryReader;
        _intentionManager = intentionManager;
        _cache = cache;
        _validator = validator;
        _uploadsCleanup = uploadsCleanup;
        _guidFactory = guidFactory;
        _avatarBroadcaster = avatarBroadcaster;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<GeneralUser> users, PagingResult paging)> GetAsync(
        PagingQuery query,
        UserActivityFilter filter,
        string? search = null,
        UserRole? role = null,
        UserSort sort = UserSort.Name)
    {
        if (filter == UserActivityFilter.Pending)
        {
            _intentionManager.ThrowIfForbidden(UserIntention.ViewPendingUsers);
        }

        // One instance for both reads, so the total and the page describe the
        // same set.
        var userFilter = new UserFilter
        {
            Activity = filter,
            Search = search,
            Role = role,
            Sort = sort
        };

        var totalCount = await _repository.CountUsersAsync(userFilter);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);
        var users = await _repository.GetUsersAsync(paging, userFilter);
        return (users, paging.Result);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> GetAsync(string username)
    {
        var user = await _repository.GetUserAsync(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.UserNotFoundByUsername(username));
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> GetAsync(Guid userId)
    {
        var user = await _repository.GetUserAsync(userId);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.UserNotFoundById(userId));
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> GetCurrentAsync()
    {
        var identity = _identityProvider.Current;
        if (!identity.User.IsAuthenticated)
        {
            throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
        }

        return await GetAsync(identity.User.UserId);
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetDetailsAsync(string username)
    {
        var normalizedUsername = username.ToLowerInvariant();
        var user = await _cache.GetOrCreateAsync(
            $"user_details_{normalizedUsername}",
            () => _repository.GetUserDetailsAsync(username),
            CachePolicy.Medium);

        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.UserNotFoundByUsername(username));
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetDetailsAsync(Guid userId)
    {
        var user = await _cache.GetOrCreateAsync(
            $"user_details_{userId}",
            () => _repository.GetUserDetailsAsync(userId),
            CachePolicy.Medium);

        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.UserNotFoundById(userId));
        }

        return user;
    }

    /// <inheritdoc />
    public Task<IEnumerable<GeneralUser>> GetByRoleAsync(UserRole role) =>
        _cache.GetOrCreateAsync(
            $"users_by_role_{role}",
            () => _repository.GetUsersByRoleAsync(role),
            CachePolicy.LongLived);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<UsernameHistoryEntry>> GetUsernameHistoryAsync(Guid userId) =>
        _usernameHistoryReader.GetByUserIdAsync(userId);

    /// <inheritdoc />
    public async Task<UserDetails> UpdateAsync(UpdateUser updateUser)
    {
        await _validator.ValidateAndThrowAsync(updateUser);
        var user = await GetAsync(updateUser.Username);
        _intentionManager.ThrowIfForbidden(UserIntention.Edit, user);

        var userEntityUpdate = new UpdateUserEntity
        {
            UserId = user.UserId,
            Status = updateUser.Status?.Trim(),
            UpdateStatus = updateUser.Status != null,
            Name = updateUser.Name?.Trim(),
            UpdateName = updateUser.Name != null,
            Location = updateUser.Location?.Trim(),
            UpdateLocation = updateUser.Location != null,
            Info = updateUser.Info?.Trim(),
            UpdateInfo = updateUser.Info != null,
            RatingDisabled = updateUser.RatingDisabled.HasValue
                ? Optional<bool>.WithValue(updateUser.RatingDisabled.Value)
                : default,
            ShowBirthday = updateUser.ShowBirthday.HasValue
                ? Optional<bool>.WithValue(updateUser.ShowBirthday.Value)
                : default
        };

        // Handle avatar from presigned URL upload
        if (updateUser.AvatarUploadId.HasValue)
        {
            var confirmedUploadId = await _repository.GetConfirmedAvatarUpload(
                user.UserId, updateUser.AvatarUploadId.Value);

            if (confirmedUploadId == null)
            {
                throw new HttpBadRequestException(
                    new Dictionary<string, string>
                    {
                        [nameof(updateUser.AvatarUploadId)] = "Загруженный аватар не найден или не подтвержден"
                    });
            }

            userEntityUpdate.AvatarUploadId = Optional<Guid>.WithValue(confirmedUploadId.Value);

            // Link upload to user entity and mark old uploads as obsolete
            await _repository.LinkAvatarUpload(user.UserId, confirmedUploadId.Value);
            await _uploadsCleanup.CollectObsoleteAsync(user.UserId, UploadType.UserAvatar);

            // Broadcast to open tabs so avatars in chats/comments
            // refresh without a reload. Best-effort, do not fail if SignalR is down.
            // The payload contains only userId — the client reloads its own data.
            await _avatarBroadcaster.BroadcastAvatarChangedAsync(user.UserId);
        }

        // Handle contacts replacement (if provided, replace all contacts)
        if (updateUser.Contacts != null)
        {
            var newContacts = updateUser.Contacts.Select((c, i) => new UserContactEntity
            {
                UserContactId = _guidFactory.Create(),
                UserId = user.UserId,
                ContactType = c.ContactType.Trim(),
                ContactValue = c.ContactValue.Trim(),
                SortOrder = c.SortOrder > 0 ? c.SortOrder : i
            });
            await _repository.ReplaceUserContacts(user.UserId, newContacts);
        }

        var settingsEntityUpdate = new UpdateUserSettingsEntity
        {
            UserId = user.UserId
        };

        // Update settings if provided
        if (updateUser.Settings != null)
        {
            settingsEntityUpdate.Theme = Optional<Theme>.WithValue(updateUser.Settings.Theme);

            if (updateUser.Settings.Paging != null)
            {
                settingsEntityUpdate.CommentsPerPage = Optional<int>.WithValue(updateUser.Settings.Paging.CommentsPerPage);
                settingsEntityUpdate.TopicsPerPage = Optional<int>.WithValue(updateUser.Settings.Paging.TopicsPerPage);
                settingsEntityUpdate.MessagesPerPage = Optional<int>.WithValue(updateUser.Settings.Paging.MessagesPerPage);
                settingsEntityUpdate.PostsPerPage = Optional<int>.WithValue(updateUser.Settings.Paging.PostsPerPage);
                settingsEntityUpdate.EntitiesPerPage = Optional<int>.WithValue(updateUser.Settings.Paging.EntitiesPerPage);
            }
        }

        await _repository.UpdateUser(userEntityUpdate, settingsEntityUpdate);

        // Invalidate cache for both username and userId lookups
        await _cache.InvalidateAsync($"user_details_{updateUser.Username.ToLowerInvariant()}");
        await _cache.InvalidateAsync($"user_details_{user.UserId}");

        return await GetDetailsAsync(updateUser.Username);
    }

    /// <inheritdoc />
    public async Task RemoveAvatarAsync(Guid userId)
    {
        // Intent check: only the user themselves (or an admin) may reset the avatar.
        var user = await GetAsync(userId);
        _intentionManager.ThrowIfForbidden(UserIntention.Edit, user);

        await _repository.UnlinkAvatarUpload(userId);

        // Best-effort cleanup (S3 + DB cleanup is done by the background GC worker).
        await _uploadsCleanup.CollectObsoleteAsync(userId, UploadType.UserAvatar);

        await _cache.InvalidateAsync($"user_details_{userId}");
        await _cache.InvalidateAsync($"user_{user.Username}");

        // Push to open tabs — the avatar is now null/default.
        await _avatarBroadcaster.BroadcastAvatarChangedAsync(userId);
    }

    // ═══ IUserLookupService ═══

    /// <inheritdoc />
    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default) =>
        await _repository.FindUserIdAsync(username) != null;

    /// <inheritdoc />
    public Task<bool> UserExistsAsync(string username, CancellationToken ct = default) =>
        UsernameExistsAsync(username, ct);

    /// <inheritdoc />
    public async Task<(bool Found, Guid UserId)> FindUserIdAsync(string username, CancellationToken ct = default)
    {
        var userId = await _repository.FindUserIdAsync(username);
        return userId.HasValue ? (true, userId.Value) : (false, Guid.Empty);
    }
}
