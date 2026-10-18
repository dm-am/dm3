using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Warnings;

/// <inheritdoc />
internal class WarningService : IWarningService
{
    /// <summary>
    /// Said by the reads that look for the warning and by the writes that find it
    /// gone, so it is written once rather than at each site.
    /// </summary>
    private const string WarningNotFound = "Предупреждение не найдено";

    private readonly IValidator<CreateWarning> _createValidator;
    private readonly IValidator<ChangeWarningPoints> _changePointsValidator;
    private readonly IWarningRepository _warningRepository;
    private readonly IBanRepository _banRepository;
    private readonly IBanService _banService;
    private readonly IWarningEntityResolver _entityResolver;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;

    /// <inheritdoc />
    public WarningService(
        IValidator<CreateWarning> createValidator,
        IValidator<ChangeWarningPoints> changePointsValidator,
        IWarningRepository warningRepository,
        IBanRepository banRepository,
        IBanService banService,
        IWarningEntityResolver entityResolver,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IEventProducer eventProducer)
    {
        _createValidator = createValidator;
        _changePointsValidator = changePointsValidator;
        _warningRepository = warningRepository;
        _banRepository = banRepository;
        _banService = banService;
        _entityResolver = entityResolver;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Warning>> GetUserWarnings(string username, CancellationToken ct = default)
    {
        try
        {
            var user = await _userLookupService.GetAsync(username);
            return await _warningRepository.GetUserWarnings(user.UserId, ct);
        }
        catch (HttpException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // See BanService: an empty list answers "no such user" and nothing
            // else. Every other failure belongs to ErrorHandlingMiddleware.
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Warning> Warnings, PagingResult Paging)> GetLog(
        string? username, PagingQuery paging, CancellationToken ct = default)
    {
        var (warnings, page) = await ReadLog(username, paging, ct);

        // The open log shows the offending text too, as DM2's did, so it needs
        // the place and whether a guest could follow the link there: resolved
        // for the rows of the page, which is all a reader can see at once.
        return (await DescribeEntities(warnings, ct), page);
    }

    /// <summary>
    /// A page of the log as stored, before anything is derived for it.
    /// </summary>
    private async Task<(IReadOnlyList<Warning> Warnings, PagingResult Paging)> ReadLog(
        string? username, PagingQuery paging, CancellationToken ct)
    {
        Guid? targetUserId = null;
        if (!string.IsNullOrEmpty(username))
        {
            try
            {
                targetUserId = (await _userLookupService.GetAsync(username)).UserId;
            }
            catch (HttpException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                // The answer GetUserWarnings already gives: an empty page says "no
                // such user" and nothing else, and the filter line above the list
                // shows whose name was asked for.
                return ([], PagingResult.Empty(paging.Take));
            }
        }

        var (warnings, total) = await _warningRepository.GetLog(targetUserId, paging, ct);
        return (warnings, new PagingData(paging, PagingPolicy.DefaultPageSize, total).Result);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Warning> Warnings, PagingResult Paging)> GetAllWarnings(
        string? username, PagingQuery paging, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Список предупреждений доступен модераторам");
        }

        var (warnings, page) = await ReadLog(username, paging, ct);

        // Whether the row offers the points to be changed is decided here and
        // not on the client: the rule reads the role of the caller and the author
        // of the warning, and a client that guessed at it would show the control
        // to a moderator the server then refuses.
        foreach (var warning in warnings)
        {
            warning.CanChangePoints = MayChangePoints(warning, currentUser);
        }

        // For the rows of the page only: paying for these queries over the whole
        // history bought a moderator a wait for rows no screen shows.
        return (await DescribeEntities(warnings, ct), page);
    }

    /// <inheritdoc />
    public async Task<Warning> CreateWarning(CreateWarning createWarning, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createWarning, ct);

        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Выносить предупреждения может только модератор");
        }

        var targetUser = await _userLookupService.GetAsync(createWarning.Username);

        // The rank rule of every action against a person (ModerationRank): a
        // moderator warns those below him and not a colleague, a senior moderator
        // or an administrator. The screens offer the button by the same formula,
        // so this refusal answers a request no screen sends.
        if (!ModerationRank.Reaches(currentUser.Role, targetUser.Role))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Предупреждение можно выдать только пользователю с ролью ниже вашей");
        }

        var entityType = ParseEntityType(createWarning.EntityType);

        // The object of a warning on a profile is the warned user himself, and
        // nothing the client sends changes that: the identifier it named is not
        // read, so the snapshot below cannot be taken of somebody else's profile.
        var entityId = entityType == WarningEntityType.Profile
            ? targetUser.UserId
            : createWarning.EntityId ?? Guid.Empty;

        // Game content is outside moderation, so no warning names a post: no
        // screen offers the button and the resolver refuses to copy one. Saying
        // so out loud, because the quiet version stored a warning that pointed at
        // a game post and carried no evidence at all.
        if (entityType == WarningEntityType.Post)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["entityType"] = "Игровой контент модерации не подлежит"
            });
        }

        // The evidence is taken here, before anything else can change it. A
        // warning points at content its author is free to edit afterwards, and no
        // edit history on the site keeps the previous text, so a warning that
        // stored only the reference lost what it was given for the moment the
        // author rewrote the sentence. A profile is taken the same way and for the
        // same reason: the avatar, the status and "О себе" are the warned user's
        // to change the next minute, and the picture is kept by the warning even
        // after he replaces it.
        ProfileCapture? profile = null;
        string? snapshot = null;
        if (entityType == WarningEntityType.Profile)
        {
            profile = await _entityResolver.CaptureProfile(entityId, ct);
            snapshot = profile?.Info;
        }
        else if (entityId != Guid.Empty)
        {
            snapshot = await _entityResolver.CaptureSnapshot(entityType, entityId, ct);
        }

        var entity = new CreateWarningEntity
        {
            WarningId = _guidFactory.Create(),
            TargetUserId = targetUser.UserId,
            AuthorId = currentUser.UserId,
            EntityId = entityId,
            EntityType = entityType,
            // 0 points = verbal warning: recorded, but adds nothing to the sum
            Points = Math.Clamp(createWarning.Points, 0, WarningPolicy.MaxPointsPerWarning),
            Text = createWarning.Reason,
            EntitySnapshot = snapshot,
            ProfileStatusSnapshot = profile?.Status,
            ProfileAvatarUploadId = profile?.AvatarUploadId,
            CreatedUtc = _dateTimeProvider.Now
        };

        // Read before the write. The automatic ban fires on the crossing of the
        // threshold, and once the row is in there is no longer a way to tell
        // whether it was this warning that carried the reader over it.
        var pointsBefore = await _warningRepository.GetUserWarningPoints(targetUser.UserId, ct);

        var warning = await _warningRepository.Create(entity, ct);

        // This event is the only thing that tells the warned user anything at
        // all: no screen interrupts them, and points accumulate towards a ban in
        // silence. Sent after the write so the generator finds the warning.
        await _eventProducer.SendAsync(EventType.WarningIssued, warning.WarningId);

        await EvaluateAutomaticBan(targetUser.UserId, pointsBefore, ct);

        // By the same single formula the list uses: the issuer may correct his own,
        // so the row he gets back must not say otherwise.
        warning.CanChangePoints = MayChangePoints(warning, currentUser);
        return (await DescribeEntities([warning], ct)).Single();
    }

    /// <summary>
    /// Fill in what is true about the offending object right now: where it is,
    /// when it was written, and whether a guest could follow the link to it
    /// from the open log.
    /// </summary>
    /// <remarks>
    /// Derived rather than stored, and derived for the whole page in one pass:
    /// an address changes when the object moves and a place can close, so a
    /// column holding either would be a copy that goes stale without anything
    /// writing to it. An edit of the object after the warning is not read: the
    /// log shows the text the warning was issued for, which is the snapshot.
    ///
    /// A warning on a profile is left as it is. Its object is the warned user,
    /// who has no place on the site to be found in and no address the log needs
    /// (the reader links the profile by the user), so it is neither sent to the
    /// resolver nor closed as a text nothing places, which its "О себе" in the
    /// snapshot would otherwise make it.
    /// </remarks>
    private async Task<IReadOnlyList<Warning>> DescribeEntities(
        IEnumerable<Warning> warnings, CancellationToken ct)
    {
        var list = warnings as IReadOnlyList<Warning> ?? warnings.ToList();
        var requests = list
            .Where(w => w.EntityId != Guid.Empty &&
                        w.EntityType != WarningEntityType.Unknown &&
                        w.EntityType != WarningEntityType.Profile)
            .Select(w => new WarningEntityRequest
            {
                WarningId = w.WarningId,
                EntityId = w.EntityId,
                EntityType = w.EntityType
            })
            .ToList();

        var states = requests.Count == 0
            ? new Dictionary<Guid, WarningEntityState>()
            : await _entityResolver.ResolveStates(requests, ct);
        foreach (var warning in list.Where(w => w.EntityType != WarningEntityType.Profile))
        {
            if (!states.TryGetValue(warning.WarningId, out var state))
            {
                // A copy of a text nothing can place has no place to lead to: the
                // open log shows the text and leaves the link out.
                if (warning.EntitySnapshot != null)
                {
                    warning.LogClosure = WarningLogClosure.NotFound;
                }

                continue;
            }

            warning.EntityUrl = state.Url;
            warning.PlaceKind = state.PlaceKind;
            warning.PlaceTitle = state.PlaceTitle;
            warning.EntityCreatedUtc = state.CreatedUtc;
            warning.LogClosure = state.LogClosure;
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<Warning> ChangeWarningPoints(
        Guid warningId, int? points, string text, CancellationToken ct = default)
    {
        await _changePointsValidator.ValidateAndThrowAsync(
            new ChangeWarningPoints { Points = points, Reason = text }, ct);

        // Past the validator the value is there: it refuses a body that left it out.
        var newPoints = points!.Value;

        var currentUser = _identityProvider.Current.User;
        var warning = await _warningRepository.Get(warningId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, WarningNotFound);

        // The rule is not a rank alone, so the refusal cannot name a rank: an
        // ordinary moderator may correct what he wrote himself and may not touch
        // anybody else's, and the general sentence is the only one true of both
        // halves.
        if (!MayChangePoints(warning, currentUser))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

        // Same reading as in CreateWarning, and for the same reason: raising the
        // points of an old warning can carry a reader over the threshold exactly
        // as a fresh warning can, and only the total before the write says whether
        // it did.
        var pointsBefore = await _warningRepository.GetUserWarningPoints(warning.TargetUser.UserId, ct);

        var changed = await _warningRepository.ChangePoints(
            warningId, newPoints, currentUser.UserId, _dateTimeProvider.Now, text.Trim(), ct);
        if (!changed)
        {
            throw new HttpException(HttpStatusCode.NotFound, WarningNotFound);
        }

        // The correction is as silent as the warning was before it got an event of
        // its own: no screen interrupts the person it was issued to, and the points
        // it costs him change without a word. Sent after the write, like the one
        // CreateWarning sends, so the generator reads the row as it now stands.
        await _eventProducer.SendAsync(EventType.WarningPointsChanged, warningId);

        await EvaluateAutomaticBan(warning.TargetUser.UserId, pointsBefore, ct);

        var updated = await _warningRepository.Get(warningId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, WarningNotFound);
        updated.CanChangePoints = MayChangePoints(updated, currentUser);
        return (await DescribeEntities([updated], ct)).Single();
    }

    /// <inheritdoc />
    public async Task DeleteProfilePicture(Guid warningId, CancellationToken ct = default)
    {
        // The right to delete a file of somebody else (UploadIntentionResolver):
        // a moderator may delete the current avatar of anybody already, and the
        // avatar of the moment of a warning is the same file a little later.
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Убрать картинку из записи лога может только модератор");
        }

        // A party does not rule on his own case, as with tickets. The picture of a
        // warning issued to this moderator, before his promotion or by a higher
        // rank, is his own avatar: the mark would carry the owner's name, and the
        // log and the orphan sweeper read such a mark as the owner's own doing, so
        // the picture would stay in the log while a success said it was gone, and
        // his current avatar, if it is still that file, would leave his profile.
        var targetUserId = await _warningRepository.GetTargetUserId(warningId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, WarningNotFound);
        if (targetUserId == currentUser.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Убрать картинку из записи лога о вас может только другой модератор");
        }

        var found = await _warningRepository.DeleteProfilePicture(
            warningId, currentUser.UserId, _dateTimeProvider.Now, ct);
        if (!found)
        {
            throw new HttpException(HttpStatusCode.NotFound, WarningNotFound);
        }
    }

    /// <summary>
    /// Hands the account over to the automatic ban when this write is what
    /// carried its total across the threshold.
    /// </summary>
    /// <remarks>
    /// The crossing, not the standing: a reader who is already at six was carried
    /// there by an earlier write and has either served the automatic window or is
    /// inside it, and firing on the standing would ban him again at every touch of
    /// his record for as long as the points live. Whether the ban is actually
    /// issued is <see cref="IBanService.IssueAutomaticBan" />'s to say - it
    /// refuses over a ban already running, and over the accounts no ban reaches.
    /// </remarks>
    private async Task EvaluateAutomaticBan(Guid targetUserId, int pointsBefore, CancellationToken ct)
    {
        if (pointsBefore >= WarningPolicy.AutoBanPointsThreshold)
        {
            return;
        }

        var pointsAfter = await _warningRepository.GetUserWarningPoints(targetUserId, ct);
        if (pointsAfter < WarningPolicy.AutoBanPointsThreshold)
        {
            return;
        }

        await _banService.IssueAutomaticBan(targetUserId, ct);
    }

    /// <summary>
    /// Who may move the points of a warning: a senior moderator over any of
    /// them, and the moderator who issued this one over his own.
    /// </summary>
    /// <remarks>
    /// One formula for the answer the moderation list shows on every row and for
    /// the check the write makes, because two copies of it would let the list
    /// offer what the write refuses.
    /// </remarks>
    private static bool MayChangePoints(Warning warning, AuthenticatedUser currentUser) =>
        currentUser.Role >= UserRole.SeniorModerator ||
        (currentUser.Role >= UserRole.Moderator &&
         warning.Author != null && warning.Author.UserId == currentUser.UserId);

    /// <inheritdoc />
    public async Task<int> GetUserWarningPoints(string username, CancellationToken ct = default)
    {
        try
        {
            var user = await _userLookupService.GetAsync(username);
            return await _warningRepository.GetUserWarningPoints(user.UserId, ct);
        }
        catch (HttpException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // Zero points is what an unblemished profile looks like, so this
            // catch may only stand for a user who does not exist.
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Violator>> GetViolators(
        ViolatorsFilter filter = ViolatorsFilter.All, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Список нарушителей доступен модераторам");
        }

        var pointsSummaries = await _warningRepository.GetActiveWarningSummaries(ct);
        var activeBans = await _banRepository.GetAllActiveBans(ct);

        // A user can theoretically have several overlapping bans; show the longest one
        var bansByUser = activeBans
            .GroupBy(b => b.TargetUser.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.EndedUtc).First());

        // The bans are read past BanService, so the flag every other ban list
        // carries is set here, by the same rule the lift is refused by: read
        // straight from the repository it said "no" to everybody.
        foreach (var ban in bansByUser.Values)
        {
            ban.CanLift = _banService.MayLift(ban);
        }

        var violators = new Dictionary<Guid, Violator>();
        foreach (var summary in pointsSummaries)
        {
            violators[summary.User.UserId] = new Violator
            {
                User = summary.User,
                Points = summary.Points,
                LastWarningUtc = summary.LastWarningUtc,
                ActiveBan = bansByUser.GetValueOrDefault(summary.User.UserId)
            };
        }

        // Banned users without active points still count as violators
        foreach (var (userId, ban) in bansByUser)
        {
            if (!violators.ContainsKey(userId))
            {
                violators[userId] = new Violator
                {
                    User = ban.TargetUser,
                    Points = 0,
                    LastWarningUtc = null,
                    ActiveBan = ban
                };
            }
        }

        var filtered = filter switch
        {
            ViolatorsFilter.Banned => violators.Values.Where(v => v.ActiveBan != null),
            ViolatorsFilter.PointsOnly => violators.Values.Where(v => v.ActiveBan == null && v.Points > 0),
            _ => violators.Values.AsEnumerable()
        };

        return filtered
            .OrderByDescending(v => v.Points)
            .ThenByDescending(v => v.LastWarningUtc ?? DateTimeOffset.MinValue)
            .ToList();
    }

    private static WarningEntityType ParseEntityType(string? entityType)
    {
        if (string.IsNullOrEmpty(entityType))
        {
            return WarningEntityType.Unknown;
        }

        return entityType.ToLowerInvariant() switch
        {
            "comment" => WarningEntityType.Comment,
            "message" => WarningEntityType.Message,
            "post" => WarningEntityType.Post,
            "topic" => WarningEntityType.Topic,
            "profile" => WarningEntityType.Profile,
            _ => WarningEntityType.Unknown
        };
    }
}
