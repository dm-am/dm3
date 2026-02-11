using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Administration;

namespace DM.Services.Community.BusinessProcesses.Moderation.Warnings;

/// <inheritdoc />
internal class BanService : IBanService
{
    private readonly IBanRepository _banRepository;
    private readonly IUserReadingRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public BanService(
        IBanRepository banRepository,
        IUserReadingRepository userRepository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _banRepository = banRepository;
        _userRepository = userRepository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ban>> GetUserBans(string login, CancellationToken ct = default)
    {
        var user = await _userRepository.GetUser(login);
        if (user == null)
        {
            return Enumerable.Empty<Ban>();
        }
        return await _banRepository.GetUserBans(user.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<Ban?> GetActiveBan(string login, CancellationToken ct = default)
    {
        var user = await _userRepository.GetUser(login);
        if (user == null)
        {
            return null;
        }
        return await _banRepository.GetActiveBan(user.UserId, ct);
    }

    /// <inheritdoc />
    public Task<IEnumerable<Ban>> GetAllActiveBans(CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can view all bans");
        }

        // Would need to implement GetAllActive in repository
        return Task.FromResult(Enumerable.Empty<Ban>());
    }

    /// <inheritdoc />
    public async Task<Ban> CreateBan(CreateBan createBan, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;

        // Voluntary bans can be created by the user themselves
        if (!createBan.IsVoluntary && currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can create bans");
        }

        var targetUser = await _userRepository.GetUser(createBan.UserLogin);
        if (targetUser == null)
        {
            throw new ArgumentException($"User {createBan.UserLogin} not found");
        }

        // Check if user is already banned
        var existingBan = await _banRepository.GetActiveBan(targetUser.UserId, ct);
        if (existingBan != null)
        {
            throw new InvalidOperationException($"User {createBan.UserLogin} is already banned until {existingBan.EndedUtc}");
        }

        var now = _dateTimeProvider.Now;
        DateTimeOffset endedUtc;

        if (createBan.ExpiresUtc.HasValue)
        {
            endedUtc = createBan.ExpiresUtc.Value;
        }
        else if (createBan.DurationHours.HasValue)
        {
            endedUtc = now.AddHours(createBan.DurationHours.Value);
        }
        else
        {
            // Permanent ban - set to far future
            endedUtc = now.AddYears(100);
        }

        var ban = new Ban
        {
            BanId = _guidFactory.Create(),
            UserId = targetUser.UserId,
            ModeratorId = createBan.IsVoluntary ? targetUser.UserId : currentUser.UserId,
            StartedUtc = now,
            EndedUtc = endedUtc,
            Comment = createBan.Comment,
            IsVoluntary = createBan.IsVoluntary,
            AccessRestrictionPolicy = AccessPolicy.FullBan,
            IsRemoved = false
        };

        return await _banRepository.Create(ban, ct);
    }

    /// <inheritdoc />
    public async Task LiftBan(Guid banId, string? reason = null, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can lift bans");
        }

        await _banRepository.Remove(banId, ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsUserBanned(string login, CancellationToken ct = default)
    {
        var user = await _userRepository.GetUser(login);
        if (user == null)
        {
            return false;
        }
        return await _banRepository.IsUserBanned(user.UserId, ct);
    }
}
