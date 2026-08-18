using System;
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

    /// <inheritdoc />
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
    public async Task<IEnumerable<BlacklistEntry>> GetMyBlacklist(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetBlacklist(userId, ct);
    }

    /// <inheritdoc />
    public async Task<UserBlacklistSettings> GetSettings(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetSettings(userId, ct);
    }

    /// <inheritdoc />
    public async Task<UserBlacklistSettings> UpdateSettings(UserBlacklistSettings settings, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.UpdateSettings(userId, settings, ct);
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
            throw new HttpException(HttpStatusCode.BadRequest, "Нельзя заблокировать самого себя");
        }

        // Check if already blocked
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
            return; // User doesn't exist, nothing to unblock
        }

        var existing = await _repository.Find(userId, userToUnblockId.Value, ct);
        if (existing != null)
        {
            await _repository.Delete(existing.Id, ct);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsBlocked(string username, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var blockedUserId = await _userRepository.FindUserIdAsync(username);

        if (blockedUserId == null)
        {
            return false;
        }

        return await _repository.IsBlockedAsync(userId, blockedUserId.Value, ct);
    }

    /// <inheritdoc />
    public async Task<BlockStatus> GetBlockStatus(Guid targetUserId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        var youBlockedThem = await _repository.IsBlockedAsync(userId, targetUserId, ct);
        var theyBlockedYou = await _repository.IsBlockedAsync(targetUserId, userId, ct);

        // Don't reveal if they blocked you - privacy protection
        // Show generic message instead of "TheyBlockedYou"
        return (youBlockedThem, theyBlockedYou) switch
        {
            (true, _) => new BlockStatus { CanCommunicate = false, Reason = "YouBlockedThem" },
            (false, true) => new BlockStatus { CanCommunicate = false, Reason = "CannotCommunicate" },
            _ => new BlockStatus { CanCommunicate = true, Reason = null }
        };
    }

}
