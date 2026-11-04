using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;

namespace DM.Domain.Personal.Features.Blacklists;

/// <inheritdoc />
internal class UserBlacklistService : IUserBlacklistService
{
    private readonly IUserBlacklistRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserBlacklistService(
        IUserBlacklistRepository repository,
        IUserRepository userRepository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _userRepository = userRepository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<IEnumerable<BlacklistEntry>> GetMyBlacklist(CancellationToken ct = default) =>
        _repository.GetBlacklist(_identityProvider.Current.User.UserId, ct);

    /// <inheritdoc />
    public Task<UserBlacklistSettings> GetSettings(CancellationToken ct = default) =>
        _repository.GetSettingsAsync(_identityProvider.Current.User.UserId, ct);

    /// <inheritdoc />
    public async Task<UserBlacklistSettings> UpdateSettings(UserBlacklistSettings settings, CancellationToken ct = default)
    {
        await _repository.UpdateSettings(_identityProvider.Current.User.UserId, settings, ct);
        return settings;
    }

    /// <inheritdoc />
    public async Task<BlacklistEntry> Block(OperateUserBlacklistLink dto, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var userToBlockId = await _userRepository.FindUserIdAsync(dto.Username);

        if (userToBlockId == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFound);
        }

        if (userToBlockId.Value == userId)
        {
            throw new HttpException(HttpStatusCode.BadRequest, "Нельзя внести себя в черный список");
        }

        var existing = await _repository.Find(userId, userToBlockId.Value, ct);
        if (existing != null)
        {
            return existing;
        }

        var entry = new CreateBlacklistEntryEntity
        {
            EntryId = _guidFactory.Create(),
            OwnerId = userId,
            BlockedUserId = userToBlockId.Value,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.Create(entry, ct);
    }

    /// <inheritdoc />
    public async Task Unblock(OperateUserBlacklistLink dto, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var userToUnblockId = await _userRepository.FindUserIdAsync(dto.Username);

        if (userToUnblockId == null)
        {
            return;
        }

        var existing = await _repository.Find(userId, userToUnblockId.Value, ct);
        if (existing != null)
        {
            await _repository.Delete(existing.Id, ct);
        }
    }
}
