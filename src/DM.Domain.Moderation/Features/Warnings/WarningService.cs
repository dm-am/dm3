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
using DM.Domain.Moderation.Features.Bans;

namespace DM.Domain.Moderation.Features.Warnings;

/// <inheritdoc />
internal class WarningService : IWarningService
{
    /// <summary>
    /// Said by the reads that look for the warning and by the writes that find it gone.
    /// </summary>
    private const string WarningNotFound = "Предупреждение не найдено";

    /// <summary>
    /// Said for a place a warning does not name (WarningEntityType): a game room, a
    /// conversation, a chat other than the global one, and the rest.
    /// </summary>
    private const string NotModeratedPlace = "Это место не модерируется. Предупредите пользователя из профиля";

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
    private readonly IReadOnlyCollection<IPlaceReading> _placeReadings;

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
        IEventProducer eventProducer,
        IEnumerable<IPlaceReading> placeReadings)
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
        _placeReadings = placeReadings.ToList();
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
            // An empty list answers "no such user" and no other failure.
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Warning> Warnings, PagingResult Paging)> GetLog(
        string? username, PagingQuery paging, CancellationToken ct = default)
    {
        var (warnings, page) = await ReadLog(username, paging, ct);

        // The open log shows the offending text and its place, resolved for the page
        // and for its viewer.
        var described = await DescribeEntities(warnings, ct);
        await OpenToViewer(described, ct);
        return (described, page);
    }

    /// <summary>
    /// What a guest may not read is opened to the viewer who reads its place, by
    /// the read rule of the place itself. A place he may not read and one that is
    /// not there stay closed alike.
    /// </summary>
    /// <remarks>
    /// Asked only of a place closed to a guest: a place a guest reads, everybody does.
    /// One read per closed place of the page, by its own read: a batch query would restate the rule and drift.
    /// A kind no module answers for stays closed.
    /// </remarks>
    private async Task OpenToViewer(IReadOnlyList<Warning> warnings, CancellationToken ct)
    {
        if (!_identityProvider.Current.User.IsAuthenticated)
        {
            return;
        }

        var closed = warnings
            .Where(w => w.LogClosure == WarningLogClosure.PlaceNotPublic && w.ClosedPlace != null)
            .GroupBy(w => w.ClosedPlace!.Kind);
        foreach (var kind in closed)
        {
            var reading = _placeReadings.FirstOrDefault(r => r.Kind == kind.Key);
            if (reading == null)
            {
                continue;
            }

            var readable = await reading.ReadableAsync(kind.Select(w => w.ClosedPlace!.Id).Distinct().ToList(), ct);
            foreach (var warning in kind.Where(w => readable.Contains(w.ClosedPlace!.Id)))
            {
                warning.LogClosure = warning.ClosureForReader;
            }
        }
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
                // As GetUserWarnings answers: an empty page says "no such user".
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

        // Decided here, by the rule the write refuses by, and not guessed on the client.
        foreach (var warning in warnings)
        {
            warning.CanChangePoints = MayChangePoints(warning, currentUser);
        }

        // For the rows of the page only: over the whole history these queries buy a wait
        // for rows no screen shows.
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

        // The rank rule of every action against a person (ModerationRank); the screens
        // offer the button by the same formula.
        if (!ModerationRank.Reaches(currentUser.Role, targetUser.Role))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Предупреждение можно выдать только пользователю с ролью ниже вашей");
        }

        var (entityType, entityId) = ResolveObject(createWarning, targetUser);
        var (snapshot, profile) = await CaptureEvidence(entityType, entityId, ct);

        // Only the global chat is moderated: a direct message, a group chat and the chat
        // of a game room are copied by nothing, and are refused rather than kept as a
        // warning that names a place it holds no text of.
        if (entityType == WarningEntityType.Message && snapshot == null)
        {
            throw NotModerated();
        }

        var entity = new CreateWarningEntity
        {
            WarningId = _guidFactory.Create(),
            TargetUserId = targetUser.UserId,
            AuthorId = currentUser.UserId,
            EntityId = entityId,
            EntityType = entityType,
            Points = createWarning.Points,
            Text = createWarning.Reason,
            EntitySnapshot = snapshot,
            ProfileStatusSnapshot = profile?.Status,
            ProfileAvatarUploadId = profile?.AvatarUploadId,
            CreatedUtc = _dateTimeProvider.Now
        };

        // Read before the write: the automatic ban fires on the crossing of the threshold.
        var pointsBefore = await _warningRepository.GetUserWarningPoints(targetUser.UserId, ct);

        var warning = await _warningRepository.Create(entity, ct);

        // Nothing else tells the warned user. Sent after the write, so the generator finds it.
        await _eventProducer.SendAsync(EventType.WarningIssued, warning.WarningId);

        await EvaluateAutomaticBan(targetUser.UserId, pointsBefore, ct);

        warning.CanChangePoints = MayChangePoints(warning, currentUser);
        return (await DescribeEntities([warning], ct)).Single();
    }

    /// <summary>
    /// The object of a warning: the kind the request named and the identifier of
    /// the offending object.
    /// </summary>
    private static (WarningEntityType EntityType, Guid EntityId) ResolveObject(
        CreateWarning createWarning, GeneralUser targetUser)
    {
        var entityType = ParseEntityType(createWarning.EntityType);

        // A warning on a profile is about the warned user himself, whatever the client
        // names, so the evidence cannot be taken of somebody else's profile. A warning
        // that names no kind names no object either: it is issued to the user alone.
        var entityId = entityType switch
        {
            WarningEntityType.Profile => targetUser.UserId,
            WarningEntityType.Unknown => Guid.Empty,
            _ => createWarning.EntityId ?? Guid.Empty
        };

        return (entityType, entityId);
    }

    /// <summary>
    /// A place moderation does not reach, refused out loud rather than stored without evidence.
    /// </summary>
    /// <remarks>
    /// For a violation there the warning is issued to the user, with no place and no copy of the text.
    /// </remarks>
    private static HttpBadRequestException NotModerated() => new(new Dictionary<string, string>
    {
        ["entityType"] = NotModeratedPlace
    });

    /// <summary>
    /// The evidence of a warning: a copy of the offending text, or of the profile
    /// for a warning on one.
    /// </summary>
    /// <remarks>
    /// Taken as the warning is issued: the author may rewrite the text and the warned user
    /// may change the profile the next minute, and no edit history keeps what was there.
    /// </remarks>
    private async Task<(string? Snapshot, ProfileCapture? Profile)> CaptureEvidence(
        WarningEntityType entityType, Guid entityId, CancellationToken ct)
    {
        if (entityType == WarningEntityType.Profile)
        {
            var profile = await _entityResolver.CaptureProfile(entityId, ct);
            return (profile?.Info, profile);
        }

        if (entityId == Guid.Empty)
        {
            return (null, null);
        }

        return (await _entityResolver.CaptureSnapshot(entityType, entityId, ct), null);
    }

    /// <summary>
    /// Fill in what is true about the offending object right now: where it is,
    /// when it was written, and whether a guest could follow the link to it
    /// from the open log.
    /// </summary>
    /// <remarks>
    /// Derived rather than stored, for the whole page in one pass: an address and a place
    /// change without anything writing to the warning. A warning on a profile is left as it
    /// is: its object is the warned user, whom the reader links by himself.
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
            warning.ClosedPlace = state.ClosedPlace;
            warning.ClosureForReader = state.ClosureForReader;
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<Warning> ChangeWarningPoints(
        Guid warningId, ChangeWarningPoints change, CancellationToken ct = default)
    {
        await _changePointsValidator.ValidateAndThrowAsync(change, ct);

        // The validator refuses a body that left the points out.
        var newPoints = change.Points!.Value;

        var currentUser = _identityProvider.Current.User;
        var warning = await _warningRepository.Get(warningId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, WarningNotFound);

        // Not a rank alone, so the refusal names no rank.
        if (!MayChangePoints(warning, currentUser))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

        // As in CreateWarning: raised points can carry a reader over the threshold.
        var pointsBefore = await _warningRepository.GetUserWarningPoints(warning.TargetUser.UserId, ct);

        var changed = await _warningRepository.ChangePoints(
            warningId, newPoints, currentUser.UserId, _dateTimeProvider.Now, change.Reason, ct);
        if (!changed)
        {
            throw new HttpException(HttpStatusCode.NotFound, WarningNotFound);
        }

        // Nothing else tells the warned user. Sent after the write, as in CreateWarning.
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
        // A moderator may delete the current avatar of anybody (UploadIntentionResolver),
        // and the avatar of a warning is the same file a little later.
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Убрать картинку из лога может только модератор");
        }

        // A party does not rule on his own case: the picture of a warning issued to this
        // moderator is his own avatar, and the mark would read as the owner's own doing.
        var targetUserId = await _warningRepository.GetTargetUserId(warningId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, WarningNotFound);
        if (targetUserId == currentUser.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Убрать картинку из лога о вас может только другой модератор");
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
    /// The crossing, not the standing, or every touch of a record at six would ban him
    /// again. <see cref="IBanService.IssueAutomaticBan" /> refuses over a running ban and
    /// over the accounts no ban reaches.
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
    /// Who may move the points of a warning: a senior moderator over any of them, and the
    /// moderator who issued this one over his own. The list and the write read this one formula.
    /// </summary>
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

        // Read past BanService, so the flag is set here by the rule the lift is refused by.
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

    /// <summary>
    /// The place a request names, by the name of a member of the one list of them.
    /// </summary>
    /// <remarks>
    /// By name and in any case, never by number. No name is a warning to the user alone;
    /// a name the list has not got is refused, rather than issued as one with no place.
    /// </remarks>
    private static WarningEntityType ParseEntityType(string? entityType)
    {
        if (string.IsNullOrEmpty(entityType))
        {
            return WarningEntityType.Unknown;
        }

        var name = Enum.GetNames<WarningEntityType>()
            .FirstOrDefault(n => string.Equals(n, entityType, StringComparison.OrdinalIgnoreCase));
        return name == null ? throw NotModerated() : Enum.Parse<WarningEntityType>(name);
    }
}
