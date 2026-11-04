using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Game;

/// <summary>
/// Notification generator for game closure warning.
/// Sent when a game will be closed due to inactivity.
/// Notifies: Master, Assistants
/// </summary>
internal class GameClosureWarningNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public GameClosureWarningNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.GameClosureWarning;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var gameData = await _dbContext.Games
            .Where(g => g.GameId == entityId)
            .Select(g => new
            {
                g.GameId,
                g.Title,
                g.MasterId,
                MasterUsername = g.Master!.Username,
                AssistantIds = g.Assistants.Select(a => a.UserId).ToList()
            })
            .FirstOrDefaultAsync();

        if (gameData == null)
        {
            yield break;
        }

        var usersInterested = new HashSet<Guid> { gameData.MasterId };
        usersInterested.UnionWith(gameData.AssistantIds);

        // No ActorId: the warning comes from a schedule.
        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            Metadata = new
            {
                GameId = gameData.GameId.EncodeToReadable(gameData.Title),
                GameTitle = gameData.Title,
                gameData.MasterUsername
            }
        };
    }
}
