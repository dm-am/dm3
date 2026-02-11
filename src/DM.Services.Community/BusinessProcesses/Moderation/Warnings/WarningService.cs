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
internal class WarningService : IWarningService
{
    private readonly IWarningRepository _warningRepository;
    private readonly IUserReadingRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public WarningService(
        IWarningRepository warningRepository,
        IUserReadingRepository userRepository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _warningRepository = warningRepository;
        _userRepository = userRepository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Warning>> GetUserWarnings(string login, CancellationToken ct = default)
    {
        var user = await _userRepository.GetUser(login);
        if (user == null)
        {
            return Enumerable.Empty<Warning>();
        }
        return await _warningRepository.GetUserWarnings(user.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Warning>> GetAllWarnings(string? userLogin = null, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can view all warnings");
        }

        if (!string.IsNullOrEmpty(userLogin))
        {
            return await GetUserWarnings(userLogin, ct);
        }

        // For now, return empty - would need to implement GetAll in repository
        return Enumerable.Empty<Warning>();
    }

    /// <inheritdoc />
    public async Task<Warning> CreateWarning(CreateWarning createWarning, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.Moderator)
        {
            throw new UnauthorizedAccessException("Only moderators can create warnings");
        }

        var targetUser = await _userRepository.GetUser(createWarning.UserLogin);
        if (targetUser == null)
        {
            throw new ArgumentException($"User {createWarning.UserLogin} not found");
        }

        var warning = new Warning
        {
            WarningId = _guidFactory.Create(),
            UserId = targetUser.UserId,
            ModeratorId = currentUser.UserId,
            EntityId = createWarning.EntityId ?? Guid.Empty,
            EntityType = ParseEntityType(createWarning.EntityType),
            Points = Math.Clamp(createWarning.Points, 1, 3),
            Text = createWarning.Reason,
            CreatedUtc = _dateTimeProvider.Now,
            IsRemoved = false
        };

        return await _warningRepository.Create(warning, ct);
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
    public async Task<int> GetUserWarningPoints(string login, CancellationToken ct = default)
    {
        var user = await _userRepository.GetUser(login);
        if (user == null)
        {
            return 0;
        }
        return await _warningRepository.GetUserWarningPoints(user.UserId, ct);
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
