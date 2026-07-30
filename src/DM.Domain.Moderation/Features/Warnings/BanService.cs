using FluentValidation;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
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
    private readonly IValidator<CreateBan> _createValidator;
    private readonly IEventProducer _eventProducer;

    /// <inheritdoc />
    public BanService(
        IBanRepository banRepository,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateBan> createValidator,
        IEventProducer eventProducer)
    {
        _banRepository = banRepository;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _eventProducer = eventProducer;
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
    public async Task<(IEnumerable<Ban> Bans, int TotalCount)> GetBanHistory(
        int skip, int take, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can view ban history");
        }

        return await _banRepository.GetBanHistory(skip, take, ct);
    }

    /// <inheritdoc />
    public async Task<Ban> CreateBan(CreateBan createBan, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createBan, ct);

        var currentUser = _identityProvider.Current.User;

        // Voluntary bans can be created by the user themselves
        if (!createBan.IsVoluntary && currentUser.Role < UserRole.SeniorModerator)
        {
            throw new UnauthorizedAccessException("Only senior moderators can create bans");
        }

        var targetUser = await _userLookupService.GetAsync(createBan.Username);

        if (!createBan.IsVoluntary)
        {
            // An administrator is a site owner. There is deliberately no in-app
            // authority over one: the ability of one owner to lock the other out
            // is a risk with no upside, and control over an owner lives outside
            // the application anyway.
            if (targetUser.Role >= UserRole.Admin)
            {
                throw new HttpException(HttpStatusCode.Forbidden,
                    "An administrator cannot be banned");
            }

            // Strictly below your own role. Equal-role bans let two senior
            // moderators ban each other, and a self-ban would be lifted by the
            // same person a second later.
            if (targetUser.Role >= currentUser.Role)
            {
                throw new HttpException(HttpStatusCode.Forbidden,
                    "You can only ban a user whose role is below yours");
            }
        }

        // Check if user is already banned
        var existingBan = await _banRepository.GetActiveBan(targetUser.UserId, ct);
        if (existingBan != null)
        {
            throw new HttpException(HttpStatusCode.Conflict,
                $"User {createBan.Username} is already banned until {existingBan.EndedUtc}");
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
            // Only a voluntary self-ban reaches here: the validator requires a
            // duration or an explicit expiry for every moderator-issued ban, and a
            // permanent one is expressed by the client as a hundred years.
            endedUtc = now.AddYears(Ban.PermanentYears);
        }

        // Only the two ban scopes from the doc (4.2.4.2) exist; an omitted or
        // unrecognized value falls back to FullBan so a malformed request never
        // produces a weaker ban than the safe default.
        var accessPolicy = createBan.AccessRestrictionPolicy == AccessPolicy.DemocraticBan
            ? AccessPolicy.DemocraticBan
            : AccessPolicy.FullBan;

        var entity = new CreateBanEntity
        {
            BanId = _guidFactory.Create(),
            TargetUserId = targetUser.UserId,
            AuthorId = createBan.IsVoluntary ? targetUser.UserId : currentUser.UserId,
            StartedUtc = now,
            EndedUtc = endedUtc,
            Comment = createBan.Comment,
            IsVoluntary = createBan.IsVoluntary,
            AccessRestrictionPolicy = accessPolicy
        };

        var ban = await _banRepository.Create(entity, ct);

        // Nothing else tells the target they were banned: the ban surfaces only
        // as a refusal at the next action they try. Sent after the write, so the
        // generator that reads the ban back by id finds it.
        await _eventProducer.SendAsync(EventType.BanIssued, ban.BanId);
        return ban;
    }

    /// <inheritdoc />
    public async Task LiftBan(Guid banId, string? reason = null, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.SeniorModerator)
        {
            throw new UnauthorizedAccessException("Only senior moderators can lift bans");
        }

        var ban = await _banRepository.Get(banId, ct);
        if (ban == null || ban.IsRemoved)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Ban not found");
        }

        // A democratic ban leaves the moderator role and authentication intact,
        // so without this a banned senior moderator lifts it from himself.
        if (ban.TargetUserId == currentUser.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You cannot lift your own ban");
        }

        // Permanent bans are stored with a far-future end date (see CreateBan);
        // lifting them is reserved for administrators. Voluntary self-bans are exempt.
        var isPermanent = ban.IsPermanentAt(_dateTimeProvider.Now);
        if (isPermanent && currentUser.Role < UserRole.Admin)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Only administrators can lift permanent bans");
        }

        await _banRepository.Remove(banId, currentUser.UserId, _dateTimeProvider.Now, reason, ct);
    }

}
