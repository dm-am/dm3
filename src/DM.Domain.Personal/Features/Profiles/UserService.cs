using DM.Domain.Core.Content;
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
using DM.Domain.Core.Users;
using DM.Domain.Personal.Authorization;
using FluentValidation;

namespace DM.Domain.Personal.Features.Profiles;

/// <inheritdoc />
internal class UserService : IUserService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserRepository _repository;
    private readonly IIntentionManager _intentionManager;
    private readonly ICache _cache;
    private readonly IValidator<UpdateUser> _validator;
    private readonly IGuidFactory _guidFactory;
    private readonly IRealtimeAvatarBroadcaster _avatarBroadcaster;

    public UserService(
        IIdentityProvider identityProvider,
        IUserRepository repository,
        IIntentionManager intentionManager,
        ICache cache,
        IValidator<UpdateUser> validator,
        IGuidFactory guidFactory,
        IRealtimeAvatarBroadcaster avatarBroadcaster)
    {
        _identityProvider = identityProvider;
        _repository = repository;
        _intentionManager = intentionManager;
        _cache = cache;
        _validator = validator;
        _guidFactory = guidFactory;
        _avatarBroadcaster = avatarBroadcaster;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<GeneralUser> Users, PagingResult Paging)> GetAsync(
        PagingQuery query,
        UserActivityFilter filter,
        string? search = null,
        UserRole? role = null,
        UserSort sort = UserSort.Name)
    {
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
        var paging = new PagingData(query, _identityProvider.Current.Settings.PageSize, totalCount);
        var users = await _repository.GetUsersAsync(paging, userFilter);
        return (users, paging.Result);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> GetAsync(string username) =>
        await _repository.GetUserAsync(username)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));

    /// <inheritdoc />
    public async Task<GeneralUser> GetAsync(Guid userId) =>
        await _repository.GetUserAsync(userId)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundById(userId));

    /// <inheritdoc />
    public async Task<UserDetails> GetDetailsAsync(string username) =>
        await _cache.GetOrCreateAsync(
            CacheKeys.UserDetails(username),
            () => _repository.GetUserDetailsAsync(username),
            CachePolicy.Medium)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));

    /// <inheritdoc />
    public async Task<UserDetails> GetDetailsAsync(Guid userId) =>
        await _cache.GetOrCreateAsync(
            CacheKeys.UserDetails(userId),
            () => _repository.GetUserDetailsAsync(userId),
            CachePolicy.Medium)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundById(userId));

    /// <inheritdoc />
    public async Task<UserDetails> UpdateAsync(UpdateUser updateUser)
    {
        await _validator.ValidateAndThrowAsync(updateUser);
        var user = await GetAsync(updateUser.Username);
        _intentionManager.ThrowIfForbidden(UserIntention.Edit, user);

        var userEntityUpdate = new UpdateUserEntity
        {
            UserId = user.UserId,
            Status = AuthoredText.AsTyped(updateUser.Status),
            UpdateStatus = updateUser.Status != null,
            Name = AuthoredText.AsTyped(updateUser.Name),
            UpdateName = updateUser.Name != null,
            Location = AuthoredText.AsTyped(updateUser.Location),
            UpdateLocation = updateUser.Location != null,
            Info = AuthoredText.AsTyped(updateUser.Info),
            UpdateInfo = updateUser.Info != null,
            RatingDisabled = updateUser.RatingDisabled.HasValue
                ? Optional<bool>.WithValue(updateUser.RatingDisabled.Value)
                : default,
            ShowBirthday = updateUser.ShowBirthday.HasValue
                ? Optional<bool>.WithValue(updateUser.ShowBirthday.Value)
                : default
        };

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

            await _repository.LinkAvatarUpload(user.UserId, confirmedUploadId.Value);
            await _avatarBroadcaster.BroadcastAvatarChangedAsync(user.UserId);
        }

        // Contacts, when given, replace all contacts
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

        if (updateUser.Settings != null)
        {
            settingsEntityUpdate.Theme = Optional<Theme>.WithValue(updateUser.Settings.Theme);
            settingsEntityUpdate.UpdateTimeZone = true;
            settingsEntityUpdate.TimeZoneId = updateUser.Settings.TimeZoneId;
            settingsEntityUpdate.PageSize = Optional<int>.WithValue(updateUser.Settings.PageSize);
        }

        await _repository.UpdateUser(userEntityUpdate, settingsEntityUpdate);
        await InvalidateProfile(updateUser.Username, user.UserId);

        return await GetDetailsAsync(updateUser.Username);
    }

    /// <inheritdoc />
    public async Task RemoveAvatarAsync(Guid userId)
    {
        var user = await GetAsync(userId);
        _intentionManager.ThrowIfForbidden(UserIntention.Edit, user);

        await _repository.UnlinkAvatarUpload(userId);

        await InvalidateProfile(user.Username, userId);
        await _avatarBroadcaster.BroadcastAvatarChangedAsync(userId);
    }

    /// <summary>A profile is cached by name and by identifier.</summary>
    private async Task InvalidateProfile(string username, Guid userId)
    {
        await _cache.InvalidateAsync(CacheKeys.UserDetails(username));
        await _cache.InvalidateAsync(CacheKeys.UserDetails(userId));
    }

    // IUserLookupService

    /// <inheritdoc />
    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default) =>
        await _repository.FindUserIdAsync(username) != null;

    /// <inheritdoc />
    public async Task<(bool Found, Guid UserId)> FindUserIdAsync(string username, CancellationToken ct = default)
    {
        var userId = await _repository.FindUserIdAsync(username);
        return userId.HasValue ? (true, userId.Value) : (false, Guid.Empty);
    }

    /// <inheritdoc />
    public Task<IEnumerable<UserReference>> GetReferencesAsync(IEnumerable<Guid> userIds) =>
        _repository.GetUserReferencesAsync(userIds);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<Guid>> GetRemovedAccountIdsAsync() =>
        _repository.GetRemovedAccountIdsAsync();
}
