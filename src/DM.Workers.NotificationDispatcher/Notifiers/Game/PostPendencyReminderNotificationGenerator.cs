using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Game;

/// <summary>
/// Notification generator for pendency reminders.
/// Notifies: The user who is expected to post (WaitingForUser) - reminder that they haven't posted yet.
/// </summary>
internal class PostPendencyReminderNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PostPendencyReminderNotificationGenerator(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.RoomPendencyReminder;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var pendencyData = await _dbContext.PostPendencies
            .Where(p => p.PendencyId == entityId && p.FulfilledUtc == null)
            .Select(p => new
            {
                p.PendencyId,
                p.RoomId,
                RoomTitle = p.Room.Title,
                p.Room.GameId,
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

        if (pendencyData == null)
        {
            yield break;
        }

        // WaitingForUser if set, otherwise the character owner
        var recipientId = pendencyData.WaitingForUserId ?? pendencyData.CharacterAuthorId!.Value;

        var daysPending = (int)(_dateTimeProvider.Now - pendencyData.CreatedUtc).TotalDays;

        // No ActorId: the reminder comes from a schedule.
        yield return new CreateNotification
        {
            UsersInterested = [recipientId],
            Metadata = new
            {
                PendencyId = pendencyData.PendencyId.EncodeToReadable(),
                GameId = pendencyData.GameId.EncodeToReadable(pendencyData.GameTitle),
                pendencyData.GameTitle,
                RoomId = pendencyData.RoomId.EncodeToReadable(),
                pendencyData.RoomTitle,
                pendencyData.CharacterName,
                pendencyData.CreatedByUsername,
                DaysPending = daysPending,
                IsReminder = true
            }
        };
    }
}
