using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Game;

/// <summary>
/// Notification generator for pendency reminders.
/// Notifies: The user who is expected to post (WaitingForUser) - reminder that they haven't posted yet.
/// </summary>
internal class GamePendencyReminderNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GamePendencyReminderNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.RoomPendencyReminder;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.PostPendencies
            .Where(p => p.PendencyId == entityId && p.FulfilledUtc == null)
            .Select(p => new
            {
                p.PendencyId,
                p.RoomId,
                RoomTitle = p.Room.Title,
                GameId = p.Room.GameId,
                GameTitle = p.Room.Game!.Title,
                p.CharacterId,
                CharacterName = p.Character.Name,
                p.WaitingForUserId,
                CharacterAuthorId = p.Character.AuthorId,
                p.CreatedById,
                CreatedByUsername = p.CreatedBy.Username,
                p.CreatedUtc
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            yield break;
        }

        // Determine who to notify: WaitingForUser if set, otherwise the character owner
        var recipientId = data.WaitingForUserId ?? data.CharacterAuthorId!.Value;

        // Calculate how long the pendency has been waiting
        var daysPending = (int)(DateTimeOffset.UtcNow - data.CreatedUtc).TotalDays;

        yield return new CreateNotification
        {
            UsersInterested = new[] { recipientId },
            Metadata = new
            {
                PendencyId = data.PendencyId.EncodeToReadable(),
                GameId = data.GameId.EncodeToReadable(data.GameTitle),
                GameTitle = data.GameTitle,
                RoomId = data.RoomId.EncodeToReadable(),
                RoomTitle = data.RoomTitle,
                CharacterName = data.CharacterName,
                CreatedByUsername = data.CreatedByUsername,
                DaysPending = daysPending,
                IsReminder = true
            }
        };
    }
}
