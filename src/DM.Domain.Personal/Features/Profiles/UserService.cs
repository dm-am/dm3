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
    private readonly IObsoleteUploadsCleanup _uploadsCleanup;
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public UserService(
        IIdentityProvider identityProvider,
        IUserRepository repository,
        IUsernameHistoryReader usernameHistoryReader,
        IIntentionManager intentionManager,
        ICache cache,
        IValidator<UpdateUser> validator,
        IObsoleteUploadsCleanup uploadsCleanup,
        IGuidFactory guidFactory)
    {
        _identityProvider = identityProvider;
        _repository = repository;
        _usernameHistoryReader = usernameHistoryReader;
        _intentionManager = intentionManager;
        _cache = cache;
        _validator = validator;
        _uploadsCleanup = uploadsCleanup;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<GeneralUser> users, PagingResult paging)> Get(
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

        var totalCount = await _repository.CountUsers(filter, search, role);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);
        var users = await _repository.GetUsers(paging, filter, search, role, sort);
        return (users, paging.Result);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Get(string username)
    {
        var user = await _repository.GetUser(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"Пользователь {username} не найден");
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Get(Guid userId)
    {
        var user = await _repository.GetUser(userId);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"Пользователь с ID {userId} не найден");
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> GetCurrent()
    {
        var identity = _identityProvider.Current;
        if (!identity.User.IsAuthenticated)
        {
            throw new HttpException(HttpStatusCode.Unauthorized, "User is not authenticated");
        }

        return await Get(identity.User.UserId);
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetDetails(string username)
    {
        var normalizedUsername = username.ToLowerInvariant();
        var user = await _cache.GetOrCreate(
            $"user_details_{normalizedUsername}",
            () => _repository.GetUserDetails(username),
            CachePolicy.Medium);

        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"Пользователь {username} не найден");
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetDetails(Guid userId)
    {
        var user = await _cache.GetOrCreate(
            $"user_details_{userId}",
            () => _repository.GetUserDetails(userId),
            CachePolicy.Medium);

        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"Пользователь с ID {userId} не найден");
        }

        return user;
    }

    /// <inheritdoc />
    public Task<IEnumerable<GeneralUser>> GetByRole(UserRole role) =>
        _cache.GetOrCreate(
            $"users_by_role_{role}",
            () => _repository.GetUsersByRole(role),
            CachePolicy.LongLived);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<UsernameHistoryEntry>> GetUsernameHistory(Guid userId) =>
        _usernameHistoryReader.GetByUserId(userId);

    /// <inheritdoc />
    public async Task<UserDetails> Update(UpdateUser updateUser)
    {
        await _validator.ValidateAndThrowAsync(updateUser);
        var user = await Get(updateUser.Username);
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
                        [nameof(updateUser.AvatarUploadId)] = "Avatar upload not found or not confirmed"
                    });
            }

            userEntityUpdate.AvatarUploadId = Optional<Guid>.WithValue(confirmedUploadId.Value);

            // Link upload to user entity and mark old uploads as obsolete
            await _repository.LinkAvatarUpload(user.UserId, confirmedUploadId.Value);
            await _uploadsCleanup.PrepareObsoleteForDeleting(user.UserId);
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
        await _cache.Invalidate($"user_details_{updateUser.Username.ToLowerInvariant()}");
        await _cache.Invalidate($"user_details_{user.UserId}");

        return await GetDetails(updateUser.Username);
    }

    // ═══ IUserLookupService ═══

    /// <inheritdoc />
    public async Task<bool> UsernameExists(string username, CancellationToken ct = default)
    {
        var user = await _repository.GetUser(username);
        return user != null;
    }

    /// <inheritdoc />
    public Task<bool> UserExists(string username, CancellationToken ct = default) =>
        UsernameExists(username, ct);

    /// <inheritdoc />
    public async Task<(bool Found, Guid UserId)> FindUserId(string username, CancellationToken ct = default)
    {
        var user = await _repository.GetUser(username);
        return user != null ? (true, user.UserId) : (false, Guid.Empty);
    }
}
