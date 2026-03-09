using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;

namespace DM.Domain.Moderation.Features.Warnings;

/// <inheritdoc />
internal class WarningService : IWarningService
{
    private readonly IWarningRepository _warningRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public WarningService(
        IWarningRepository warningRepository,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _warningRepository = warningRepository;
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
            var user = await _userLookupService.Get(username);
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
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can create warnings");
        }

        var targetUser = await _userLookupService.Get(createWarning.Username);

        var entity = new CreateWarningEntity
        {
            WarningId = _guidFactory.Create(),
            TargetUserId = targetUser.UserId,
            AuthorId = currentUser.UserId,
            EntityId = createWarning.EntityId ?? Guid.Empty,
            EntityType = ParseEntityType(createWarning.EntityType),
            Points = Math.Clamp(createWarning.Points, 1, 3),
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
            var user = await _userLookupService.Get(username);
            return await _warningRepository.GetUserWarningPoints(user.UserId, ct);
        }
        catch
        {
            return 0;
        }
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
