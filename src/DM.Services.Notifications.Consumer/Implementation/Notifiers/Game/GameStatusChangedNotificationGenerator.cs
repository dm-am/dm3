using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.Notifications.Dto;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Notifications.Consumer.Implementation.Notifiers.Game;

/// <summary>
/// Notification generator for game status changes
/// </summary>
internal class GameStatusChangedNotificationGenerator : INotificationGenerator
{
    private static readonly EventType[] SupportedTypes =
    {
        EventType.StatusGameActive,
        EventType.StatusGameClosed,
        EventType.StatusGameFrozen,
        EventType.StatusGameFinished
    };

    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GameStatusChangedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public bool CanResolve(EventType eventType) => SupportedTypes.Contains(eventType);

    /// <inheritdoc />
    public async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.Games
            .Where(g => g.GameId == entityId)
            .Select(g => new
            {
                g.GameId,
                g.Title,
                g.Status,
                g.MasterId,
                g.AssistantId,
                MasterLogin = g.Master!.Login,
                // Get all active players (users who own active characters)
                PlayerIds = g.Characters
                    .Where(c => !c.IsRemoved && c.Status == CharacterStatus.Active)
                    .Select(c => c.UserId)
                    .Distinct()
                    .ToList(),
                // Get all readers
                ReaderIds = g.Readers
                    .Select(r => r.UserId)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            yield break;
        }

        // Combine players and readers, exclude master (they initiated the change)
        var usersInterested = data.PlayerIds
            .Union(data.ReaderIds)
            .Where(id => id != data.MasterId)
            .ToList();

        // Add assistant if present and not the master
        if (data.AssistantId.HasValue && data.AssistantId != data.MasterId)
        {
            usersInterested.Add(data.AssistantId.Value);
        }

        usersInterested = usersInterested.Distinct().ToList();

        if (!usersInterested.Any())
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            Metadata = new
            {
                GameTitle = data.Title,
                GameId = data.GameId.EncodeToReadable(),
                NewStatus = data.Status.ToString(),
                MasterLogin = data.MasterLogin
            }
        };
    }
}
