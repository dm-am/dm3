using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Blacklist;

/// <inheritdoc />
internal class UserBlacklistService : IUserBlacklistService
{
    private readonly IUserBlacklistRepository _repository;
    private readonly IUserReadingRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public UserBlacklistService(
        IUserBlacklistRepository repository,
        IUserReadingRepository userRepository,
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
    public async Task<IEnumerable<BlacklistEntryDto>> GetMyBlacklist(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var entries = await _repository.GetBlacklist(userId, ct);
        return entries.Select(MapToDto);
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
    public async Task<BlacklistEntryDto> BlockUser(string login, string? reason = null, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var userToBlock = await _userRepository.GetUser(login);

        if (userToBlock == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "User not found");
        }

        if (userToBlock.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.BadRequest, "Cannot block yourself");
        }

        // Check if already blocked
        var existing = await _repository.Find(userId, userToBlock.UserId, ct);
        if (existing != null)
        {
            return MapToDto(existing, userToBlock.Login);
        }

        var entry = new UserBlacklist
        {
            EntryId = _guidFactory.Create(),
            OwnerId = userId,
            BlockedUserId = userToBlock.UserId,
            Reason = reason,
            CreatedUtc = _dateTimeProvider.Now
        };

        var created = await _repository.Create(entry, ct);
        return MapToDto(created, userToBlock.Login);
    }

    /// <inheritdoc />
    public async Task UnblockUser(string login, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var userToUnblock = await _userRepository.GetUser(login);

        if (userToUnblock == null)
        {
            return; // User doesn't exist, nothing to unblock
        }

        var existing = await _repository.Find(userId, userToUnblock.UserId, ct);
        if (existing != null)
        {
            await _repository.Delete(existing.EntryId, ct);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsBlocked(string login, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var user = await _userRepository.GetUser(login);

        if (user == null)
        {
            return false;
        }

        return await _repository.IsBlocked(userId, user.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<bool> CanSendMessage(Guid targetUserId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        // Check if target has blocked current user
        // Default behavior: if blocked, messages are blocked
        if (await _repository.IsBlocked(targetUserId, userId, ct))
        {
            // Default settings block direct messages
            return false;
        }

        return true;
    }

    private static BlacklistEntryDto MapToDto(UserBlacklist entry) => new()
    {
        Id = entry.EntryId,
        Login = entry.BlockedUser?.Login ?? "Unknown",
        Reason = entry.Reason,
        CreatedUtc = entry.CreatedUtc
    };

    private static BlacklistEntryDto MapToDto(UserBlacklist entry, string login) => new()
    {
        Id = entry.EntryId,
        Login = login,
        Reason = entry.Reason,
        CreatedUtc = entry.CreatedUtc
    };
}
