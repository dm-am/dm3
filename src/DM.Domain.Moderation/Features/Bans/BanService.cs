using FluentValidation;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Features.Warnings;

namespace DM.Domain.Moderation.Features.Bans;

/// <inheritdoc />
internal class BanService : IBanService
{
    private const string LiftRankRefusal = "Снимать бан может только старший модератор";

    private const string BanNotFound = "Бан не найден";

    private readonly IBanRepository _banRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateBan> _createValidator;
    private readonly IEventProducer _eventProducer;

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
            var bans = (await _banRepository.GetUserBans(user.UserId, ct)).ToList();
            MarkWhetherTheReaderMayLift(bans);
            return bans;
        }
        catch (HttpException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // An empty list answers "no such user" and no other failure.
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<Ban?> GetActiveBan(string username, CancellationToken ct = default)
    {
        try
        {
            var user = await _userLookupService.GetAsync(username);
            var ban = await _banRepository.GetActiveBan(user.UserId, ct);
            MarkWhetherTheReaderMayLift(ban);
            return ban;
        }
        catch (HttpException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // "Not banned" answers "no such user" and no other failure: the route is public.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ban>> GetAllActiveBans(CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Список банов доступен модераторам");
        }

        var bans = (await _banRepository.GetAllActiveBans(ct)).ToList();
        MarkWhetherTheReaderMayLift(bans);
        return bans;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<BanLogEntry> Entries, int TotalCount, int Skip)> GetBanHistory(
        string? username, int skip, int take, int? entry = null, CancellationToken ct = default)
    {
        // Open to everyone, a guest too, as the warning log is: what a ban was for
        // and who issued it is the site's public record. Only the lift is a role's.
        Guid? targetUserId = null;
        if (!string.IsNullOrEmpty(username))
        {
            try
            {
                targetUserId = (await _userLookupService.GetAsync(username)).UserId;
            }
            catch (HttpException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                // As in the warning log: an empty page says "no such user".
                return ([], 0, skip);
            }
        }

        // A link to an entry outlives its page number, since every new entry moves it down.
        if (entry is { } number && take > 0 &&
            await _banRepository.GetBanHistoryPosition(targetUserId, number, ct) is { } position)
        {
            skip = position / take * take;
        }

        var (entries, totalCount) = await _banRepository.GetBanHistory(targetUserId, skip, take, ct);
        MarkWhetherTheReaderMayLift(entries.Select(e => e.Ban));
        return (entries, totalCount, skip);
    }

    /// <inheritdoc />
    public async Task<Ban> CreateBan(CreateBan createBan, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createBan, ct);

        var currentUser = _identityProvider.Current.User;

        if (!createBan.IsVoluntary && currentUser.Role < UserRole.SeniorModerator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Банить может только старший модератор");
        }

        var targetUser = await _userLookupService.GetAsync(createBan.Username);
        ThrowIfOutOfReach(createBan, currentUser, targetUser);

        var existingBan = await _banRepository.GetActiveBan(targetUser.UserId, ct);
        if (existingBan != null)
        {
            // No end date: the site spells dates on the client, in the reader's time zone.
            throw new HttpException(HttpStatusCode.Conflict, $"Пользователь {createBan.Username} уже забанен");
        }

        var now = _dateTimeProvider.Now;
        var entity = new CreateBanEntity
        {
            BanId = _guidFactory.Create(),
            TargetUserId = targetUser.UserId,
            AuthorId = createBan.IsVoluntary ? targetUser.UserId : currentUser.UserId,
            StartedUtc = now,
            EndedUtc = EndOf(createBan, now),
            Comment = createBan.Comment,
            IsVoluntary = createBan.IsVoluntary,
            AccessRestrictionPolicy = createBan.AccessRestrictionPolicy
        };

        var ban = await _banRepository.Create(entity, ct);
        MarkWhetherTheReaderMayLift(ban);

        // Nothing else tells the target he is banned. A self-ban tells him nothing new, so
        // it sends nothing. Sent after the write, so the generator finds the ban.
        if (!createBan.IsVoluntary)
        {
            await _eventProducer.SendAsync(EventType.BanIssued, ban.BanId);
        }

        return ban;
    }

    private static void ThrowIfOutOfReach(
        CreateBan createBan, AuthenticatedUser currentUser, GeneralUser targetUser)
    {
        // A voluntary ban skips every rank check, so its target is the person asking.
        if (createBan.IsVoluntary && targetUser.UserId != currentUser.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Добровольный бан можно наложить только на себя");
        }

        // The rank rule of every action against a person (ModerationRank), the refusal
        // naming which half the target failed.
        if (!createBan.IsVoluntary && !ModerationRank.Reaches(currentUser.Role, targetUser.Role))
        {
            throw new HttpException(HttpStatusCode.Forbidden, targetUser.Role >= UserRole.Admin
                ? "Администратора нельзя забанить"
                : "Забанить можно только пользователя с ролью ниже вашей");
        }
    }

    /// <summary>
    /// When the ban ends: at the expiry the request names, after its duration,
    /// or never.
    /// </summary>
    private static DateTimeOffset EndOf(CreateBan createBan, DateTimeOffset now)
    {
        if (createBan.ExpiresUtc.HasValue)
        {
            return createBan.ExpiresUtc.Value;
        }

        if (createBan.DurationHours.HasValue)
        {
            return now.AddHours(createBan.DurationHours.Value);
        }

        // Only a self-ban: the validator wants a term from a moderator, whose permanent
        // ban comes as a hundred years.
        return now.AddYears(Ban.PermanentYears);
    }

    /// <inheritdoc />
    public async Task<Ban?> IssueAutomaticBan(Guid targetUserId, CancellationToken ct = default)
    {
        // A running ban already holds the reader off; a second would move its end date
        // without anybody deciding it.
        var existingBan = await _banRepository.GetActiveBan(targetUserId, ct);
        if (existingBan != null)
        {
            return null;
        }

        GeneralUser targetUser;
        try
        {
            targetUser = await _userLookupService.GetAsync(targetUserId);
        }
        catch (HttpException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // Removed after the warning was written: nobody to ban, and no 404 about a
            // write that succeeded.
            return null;
        }

        // No in-app authority over a site owner, a threshold included (as in CreateBan).
        if (targetUser.Role >= UserRole.Admin)
        {
            return null;
        }

        var now = _dateTimeProvider.Now;
        var entity = new CreateBanEntity
        {
            BanId = _guidFactory.Create(),
            TargetUserId = targetUser.UserId,
            // The system account as the author is what makes the ban read back as automatic.
            AuthorId = SystemUser.Id,
            StartedUtc = now,
            EndedUtc = now.AddHours(WarningPolicy.AutoBanDurationHours),
            Comment = WarningPolicy.AutoBanComment,
            IsVoluntary = false,
            AccessRestrictionPolicy = WarningPolicy.AutoBanPolicy
        };

        var ban = await _banRepository.Create(entity, ct);

        // The same event a moderator's ban sends: nothing else tells the reader.
        await _eventProducer.SendAsync(EventType.BanIssued, ban.BanId);

        return ban;
    }

    /// <inheritdoc />
    public bool MayLift(Ban ban) =>
        LiftRefusal(ban, _identityProvider.Current.User, _dateTimeProvider.Now) is null;

    /// <summary>
    /// Why the reader may not lift the ban, or null when he may: one formula for the
    /// flag of every ban list and for the refusal of the lift, so a list never offers
    /// what the write refuses.
    /// </summary>
    private static HttpException? LiftRefusal(Ban ban, AuthenticatedUser reader, DateTimeOffset now)
    {
        if (reader.Role < UserRole.SeniorModerator)
        {
            return new HttpException(HttpStatusCode.Forbidden, LiftRankRefusal);
        }

        if (ban.IsLifted)
        {
            return new HttpException(HttpStatusCode.NotFound, BanNotFound);
        }

        // Nothing of an ended ban is left to end, and its lift would stand in the list.
        if (!ban.IsInForceAt(now))
        {
            return new HttpException(HttpStatusCode.Conflict, "Бан уже закончился");
        }

        // A democratic ban leaves the moderator role and authentication intact,
        // so without this a banned senior moderator lifts it from himself.
        if (ban.TargetUserId == reader.UserId)
        {
            return new HttpException(HttpStatusCode.Forbidden, "Нельзя снять бан с самого себя");
        }

        // Permanent bans are stored with a far-future end date (see CreateBan);
        // lifting them is reserved for administrators. Voluntary self-bans are exempt.
        if (ban.IsPermanentAt(now) && reader.Role < UserRole.Admin)
        {
            return new HttpException(HttpStatusCode.Forbidden, "Постоянный бан может снять только администратор");
        }

        return null;
    }

    private void MarkWhetherTheReaderMayLift(IEnumerable<Ban> bans)
    {
        foreach (var ban in bans)
        {
            ban.CanLift = MayLift(ban);
        }
    }

    private void MarkWhetherTheReaderMayLift(Ban? ban)
    {
        if (ban != null)
        {
            MarkWhetherTheReaderMayLift([ban]);
        }
    }

    /// <inheritdoc />
    public async Task LiftBan(Guid banId, string reason, CancellationToken ct = default)
    {
        // Whoever may not lift any ban learns nothing about this one.
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.SeniorModerator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, LiftRankRefusal);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["reason"] = ValidationError.Empty
            });
        }

        if (reason.Trim().Length > WarningPolicy.ReasonMaxLength)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["reason"] = ValidationError.Long
            });
        }

        var ban = await _banRepository.Get(banId, ct)
                  ?? throw new HttpException(HttpStatusCode.NotFound, BanNotFound);

        var now = _dateTimeProvider.Now;
        if (LiftRefusal(ban, currentUser, now) is { } refusal)
        {
            throw refusal;
        }

        await _banRepository.Lift(banId, currentUser.UserId, now, reason, ct);
    }
}
