using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;

namespace DM.Domain.Moderation.Features.Warnings;

/// <inheritdoc />
internal class BanService : IBanService
{
    private readonly IBanRepository _banRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public BanService(
        IBanRepository banRepository,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _banRepository = banRepository;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ban>> GetUserBans(string username, CancellationToken ct = default)
    {
        try
        {
            var user = await _userLookupService.GetAsync(username);
            return await _banRepository.GetUserBans(user.UserId, ct);
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<Ban?> GetActiveBan(string username, CancellationToken ct = default)
    {
        try
        {
            var user = await _userLookupService.GetAsync(username);
            return await _banRepository.GetActiveBan(user.UserId, ct);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ban>> GetAllActiveBans(CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can view all bans");
        }

        return await _banRepository.GetAllActiveBans(ct);
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

        var targetUser = await _userLookupService.GetAsync(createBan.Username);

        // Check if user is already banned
        var existingBan = await _banRepository.GetActiveBan(targetUser.UserId, ct);
        if (existingBan != null)
        {
            throw new InvalidOperationException($"User {createBan.Username} is already banned until {existingBan.EndedUtc}");
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

        var entity = new CreateBanEntity
        {
            BanId = _guidFactory.Create(),
            TargetUserId = targetUser.UserId,
            AuthorId = createBan.IsVoluntary ? targetUser.UserId : currentUser.UserId,
            StartedUtc = now,
            EndedUtc = endedUtc,
            Comment = createBan.Comment,
            IsVoluntary = createBan.IsVoluntary,
            AccessRestrictionPolicy = AccessPolicy.FullBan
        };

        return await _banRepository.Create(entity, ct);
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
    public async Task<bool> IsUserBanned(string username, CancellationToken ct = default)
    {
        try
        {
            var user = await _userLookupService.GetAsync(username);
            return await _banRepository.IsUserBanned(user.UserId, ct);
        }
        catch
        {
            return false;
        }
    }
}
