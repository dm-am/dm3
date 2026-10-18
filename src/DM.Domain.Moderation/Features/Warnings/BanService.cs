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
            var bans = (await _banRepository.GetUserBans(user.UserId, ct)).ToList();
            MarkWhetherTheReaderMayLift(bans);
            return bans;
        }
        catch (HttpException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // The one failure an empty list is the answer to: no such user.
            // A dropped connection, a cancelled request or a mapping error is
            // not that answer, and returning [] for it showed the moderator a
            // clean record he could not tell from a real one.
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
            // Reached from the public /v1/users/{username}/bans/active route,
            // so "we could not check" used to leave as "not banned".
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
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "История банов доступна модераторам");
        }

        Guid? targetUserId = null;
        if (!string.IsNullOrEmpty(username))
        {
            try
            {
                targetUserId = (await _userLookupService.GetAsync(username)).UserId;
            }
            catch (HttpException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                // The answer the warning log gives: an empty page says "no such
                // user" and nothing else, and the search line above the list
                // shows whose name was asked for.
                return ([], 0, skip);
            }
        }

        // The page of an entry is the page of this list it falls on: a link to
        // an entry outlives the page number it had, since every new entry
        // stands above it and moves it down.
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

        // Voluntary bans can be created by the user themselves
        if (!createBan.IsVoluntary && currentUser.Role < UserRole.SeniorModerator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Банить может только старший модератор");
        }

        var targetUser = await _userLookupService.GetAsync(createBan.Username);

        // A voluntary ban skips the senior-moderator gate above and both role
        // checks below, so the target has to be the person asking for it.
        // Without this the flag alone bans anybody, an administrator included,
        // and files the ban under the victim's own name; the branch is
        // unreachable today only because both callers hard-code the flag to
        // false, which is a property of the callers and not of the rule.
        if (createBan.IsVoluntary && targetUser.UserId != currentUser.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Добровольный бан можно наложить только на себя");
        }

        // The rank rule of every action against a person (ModerationRank):
        // strictly below your own role, and never an administrator. The refusal
        // names which half the target failed: an administrator is out of reach
        // of every rank, the rest of the targets only of an equal or lower one.
        if (!createBan.IsVoluntary && !ModerationRank.Reaches(currentUser.Role, targetUser.Role))
        {
            throw new HttpException(HttpStatusCode.Forbidden, targetUser.Role >= UserRole.Admin
                ? "Администратора нельзя забанить"
                : "Забанить можно только пользователя с ролью ниже вашей");
        }

        // Check if user is already banned
        var existingBan = await _banRepository.GetActiveBan(targetUser.UserId, ct);
        if (existingBan != null)
        {
            throw new HttpException(HttpStatusCode.Conflict,
                $"Пользователь {createBan.Username} уже забанен до {existingBan.EndedUtc}");
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
        MarkWhetherTheReaderMayLift(ban);

        // Nothing else tells the target they were banned: the ban surfaces only
        // as a refusal at the next action they try. Sent after the write, so the
        // generator that reads the ban back by id finds it.
        //
        // A voluntary self-ban is the exception. The generator addresses exactly
        // one recipient - the target - and for a self-ban that is the person who
        // just requested it: the notification tells him nothing he does not know
        // and reads as a sanction imposed from outside.
        if (!createBan.IsVoluntary)
        {
            await _eventProducer.SendAsync(EventType.BanIssued, ban.BanId);
        }

        return ban;
    }

    /// <inheritdoc />
    public async Task<Ban?> IssueAutomaticBan(Guid targetUserId, CancellationToken ct = default)
    {
        // A ban already in force has already taken the reader out of public
        // speech, and the point of the automatic one is only to do that until a
        // human arrives. Stacking a second window on top would move the end date
        // a moderator is looking at without anybody having decided anything.
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
            // Nobody to ban. The path runs after a warning has already been
            // written, so an account removed between the two would otherwise
            // answer the moderator with 404 about a write that succeeded.
            return null;
        }

        // The same account CreateBan refuses by hand, refused here for the same
        // reason: there is deliberately no in-app authority over a site owner,
        // and a threshold is no exception to it.
        if (targetUser.Role >= UserRole.Admin)
        {
            return null;
        }

        var now = _dateTimeProvider.Now;
        var entity = new CreateBanEntity
        {
            BanId = _guidFactory.Create(),
            TargetUserId = targetUser.UserId,
            // Filed under the system account, which is what makes the ban read
            // back as automatic: no column says so, and inventing one would put
            // the same fact in two places.
            AuthorId = SystemUser.Id,
            StartedUtc = now,
            EndedUtc = now.AddHours(WarningPolicy.AutoBanDurationHours),
            Comment = WarningPolicy.AutoBanComment,
            IsVoluntary = false,
            AccessRestrictionPolicy = WarningPolicy.AutoBanPolicy
        };

        var ban = await _banRepository.Create(entity, ct);

        // The same event a moderator's ban sends: nothing else tells the reader
        // he is banned, and an automatic ban he is not told about is a site that
        // stopped answering for no stated reason.
        await _eventProducer.SendAsync(EventType.BanIssued, ban.BanId);

        return ban;
    }

    /// <summary>
    /// The longest reason a lift takes, the length of a points explanation.
    /// </summary>
    private const int LiftReasonMaxLength = 2000;

    private const string LiftRankRefusal = "Снимать бан может только старший модератор";

    private const string BanNotFound = "Бан не найден";

    /// <inheritdoc />
    public bool MayLift(Ban ban) =>
        LiftRefusal(ban, _identityProvider.Current.User, _dateTimeProvider.Now) is null;

    /// <summary>
    /// Why the reader may not lift the ban, or null when he may.
    /// </summary>
    /// <remarks>
    /// One formula for the flag every ban list carries and for the refusal of
    /// the lift itself, because two copies of it let the list offer what the
    /// write refuses: the page drew "Снять бан" for every running ban a senior
    /// moderator could see, his own and the permanent ones included, and the
    /// server then said no. A ban that has run out is refused too: nothing of
    /// it is left to end, and a lift written after its end would stand in the
    /// ban list as an entry about a ban that was already over.
    /// </remarks>
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

    /// <summary>
    /// Marks each ban with whether the current reader may lift it, by
    /// <see cref="MayLift" />.
    /// </summary>
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
        // The rank is asked before anything is read, as it always was: whoever
        // may not lift any ban learns nothing about this one.
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.SeniorModerator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, LiftRankRefusal);
        }

        // Required, and held to the length of the explanation a change of points
        // takes (ChangeWarningPointsValidator): both are one line of a decision
        // in the moderation list.
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["reason"] = ValidationError.Empty
            });
        }

        if (reason.Trim().Length > LiftReasonMaxLength)
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
