using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Warnings;

/// <inheritdoc />
internal class WarningService : IWarningService
{
    private readonly IValidator<CreateWarning> _createValidator;
    private readonly IWarningRepository _warningRepository;
    private readonly IBanRepository _banRepository;
    private readonly IWarningEntityResolver _entityResolver;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;

    /// <inheritdoc />
    public WarningService(
        IValidator<CreateWarning> createValidator,
        IWarningRepository warningRepository,
        IBanRepository banRepository,
        IWarningEntityResolver entityResolver,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IEventProducer eventProducer)
    {
        _createValidator = createValidator;
        _warningRepository = warningRepository;
        _banRepository = banRepository;
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
    public async Task<IEnumerable<Warning>> GetAllWarnings(string? username = null, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Список предупреждений доступен модераторам");
        }

        // The unfiltered list used to be an empty one with a note that the
        // repository method was missing: behind a Moderator+ gate and a 200, a
        // moderator opening the page saw what a website without violations looks
        // like.
        var warnings = string.IsNullOrEmpty(username)
            ? await _warningRepository.GetAllWarnings(ct)
            : await GetUserWarnings(username, ct);

        // Only the moderation list gets the address and the "edited since" mark:
        // the public profile view is trimmed to points and dates, so paying for
        // these queries there would buy nothing anyone is allowed to see.
        return await DescribeEntities(warnings, ct);
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

        var entityId = createWarning.EntityId ?? Guid.Empty;
        var entityType = ParseEntityType(createWarning.EntityType);

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
        // author rewrote the sentence.
        var snapshot = entityId == Guid.Empty
            ? null
            : await _entityResolver.CaptureSnapshot(entityType, entityId, ct);

        var entity = new CreateWarningEntity
        {
            WarningId = _guidFactory.Create(),
            TargetUserId = targetUser.UserId,
            AuthorId = currentUser.UserId,
            EntityId = entityId,
            EntityType = entityType,
            // 0 points = verbal warning: recorded, but adds nothing to the sum
            Points = Math.Clamp(createWarning.Points, 0, 6),
            Text = createWarning.Reason,
            EntitySnapshot = snapshot,
            CreatedUtc = _dateTimeProvider.Now
        };

        var warning = await _warningRepository.Create(entity, ct);

        // This event is the only thing that tells the warned user anything at
        // all: no screen interrupts them, and points accumulate towards a ban in
        // silence. Sent after the write so the generator finds the warning.
        await _eventProducer.SendAsync(EventType.WarningIssued, warning.WarningId);
        return (await DescribeEntities([warning], ct)).Single();
    }

    /// <summary>
    /// Fill in what is true about the offending object right now: where it is,
    /// and whether it was edited after the warning was issued.
    /// </summary>
    /// <remarks>
    /// Derived rather than stored, and derived for the whole page in one pass:
    /// an address changes when the object moves, and "edited since" changes every
    /// time the author touches it, so a column holding either would be a copy
    /// that goes stale without anything writing to it.
    /// </remarks>
    private async Task<IEnumerable<Warning>> DescribeEntities(
        IEnumerable<Warning> warnings, CancellationToken ct)
    {
        var list = warnings as IReadOnlyList<Warning> ?? warnings.ToList();
        var requests = list
            .Where(w => w.EntityId != Guid.Empty && w.EntityType != WarningEntityType.Unknown)
            .Select(w => new WarningEntityRequest
            {
                WarningId = w.WarningId,
                EntityId = w.EntityId,
                EntityType = w.EntityType,
                IssuedUtc = w.CreatedUtc
            })
            .ToList();

        if (requests.Count == 0)
        {
            return list;
        }

        var states = await _entityResolver.ResolveStates(requests, ct);
        foreach (var warning in list)
        {
            if (!states.TryGetValue(warning.WarningId, out var state))
            {
                continue;
            }

            warning.EntityUrl = state.Url;
            warning.EntityEditedAfterWarning = state.EditedAfterWarning;
        }

        return list;
    }

    /// <inheritdoc />
    public async Task RemoveWarning(Guid warningId, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Снимать предупреждения может только модератор");
        }

        await _warningRepository.Remove(warningId, ct);
    }

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
            _ => WarningEntityType.Unknown
        };
    }
}
