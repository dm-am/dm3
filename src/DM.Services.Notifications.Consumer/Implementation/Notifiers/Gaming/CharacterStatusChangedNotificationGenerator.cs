using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.Notifications.Dto;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Notifications.Consumer.Implementation.Notifiers.Gaming;

/// <summary>
/// Notification generator for character status changes
/// </summary>
internal class CharacterStatusChangedNotificationGenerator : INotificationGenerator
{
    private static readonly EventType[] SupportedTypes =
    {
        EventType.StatusCharacterDeclined,
        EventType.StatusCharacterAccepted,
        EventType.StatusCharacterDied,
        EventType.StatusCharacterResurrected,
        EventType.StatusCharacterLeft,
        EventType.StatusCharacterReturned
    };

    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public CharacterStatusChangedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public bool CanResolve(EventType eventType) => SupportedTypes.Contains(eventType);

    /// <inheritdoc />
    public async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.Characters
            .Where(c => c.CharacterId == entityId)
            .Select(c => new
            {
                c.CharacterId,
                c.Name,
                c.UserId,
                c.Status,
                c.GameId,
                GameTitle = c.Game.Title,
                MasterLogin = c.Game.Master.Login,
                c.Game.MasterId
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            yield break;
        }

        // Don't notify the master about their own actions
        if (data.UserId == data.MasterId)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { data.UserId },
            Metadata = new
            {
                CharacterName = data.Name,
                CharacterId = data.CharacterId.EncodeToReadable(),
                GameTitle = data.GameTitle,
                GameId = data.GameId.EncodeToReadable(),
                NewStatus = data.Status.ToString(),
                MasterLogin = data.MasterLogin
            }
        };
    }
}
