using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Warnings;

/// <inheritdoc />
internal class WarningService : IWarningService
{
    private readonly IValidator<CreateWarning> _createValidator;
    private readonly IWarningRepository _warningRepository;
    private readonly IBanRepository _banRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public WarningService(
        IValidator<CreateWarning> createValidator,
        IWarningRepository warningRepository,
        IBanRepository banRepository,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _createValidator = createValidator;
        _warningRepository = warningRepository;
        _banRepository = banRepository;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Warning>> GetUserWarnings(string username, CancellationToken ct = default)
    {
        try
        {
            var user = await _userLookupService.GetAsync(username);
            return await _warningRepository.GetUserWarnings(user.UserId, ct);
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Warning>> GetAllWarnings(string? username = null, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can view all warnings");
        }

        if (!string.IsNullOrEmpty(username))
        {
            return await GetUserWarnings(username, ct);
        }

        // For now, return empty - would need to implement GetAll in repository
        return [];
    }

    /// <inheritdoc />
    public async Task<Warning> CreateWarning(CreateWarning createWarning, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createWarning, ct);

        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can create warnings");
        }

        var targetUser = await _userLookupService.GetAsync(createWarning.Username);

        var entity = new CreateWarningEntity
        {
            WarningId = _guidFactory.Create(),
            TargetUserId = targetUser.UserId,
            AuthorId = currentUser.UserId,
            EntityId = createWarning.EntityId ?? Guid.Empty,
            EntityType = ParseEntityType(createWarning.EntityType),
            // 0 points = verbal warning: recorded, but adds nothing to the sum
            Points = Math.Clamp(createWarning.Points, 0, 6),
            Text = createWarning.Reason,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _warningRepository.Create(entity, ct);
    }

    /// <inheritdoc />
    public async Task RemoveWarning(Guid warningId, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can remove warnings");
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
        catch
        {
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
            throw new UnauthorizedAccessException("Only moderators can view violators");
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
