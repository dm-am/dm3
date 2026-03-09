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
/// Notification generator when a post pendency is fulfilled.
/// Notifies: The user who created the pendency (CreatedBy)
/// </summary>
internal class GamePendencyFulfilledNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GamePendencyFulfilledNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.RoomPendencyFulfilled;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.PostPendencies
            .Where(p => p.PendencyId == entityId)
            .Select(p => new
            {
                p.PendencyId,
                p.RoomId,
                RoomTitle = p.Room.Title,
                GameId = p.Room.GameId,
                GameTitle = p.Room.Game!.Title,
                CharacterName = p.Character.Name,
                p.WaitingForUserId,
                CharacterAuthorId = p.Character.AuthorId,
                CharacterOwnerUsername = p.Character.Author!.Username,
                p.CreatedById
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            yield break;
        }

        // The character owner is the one who fulfilled the pendency
        var fulfilledByUsername = data.CharacterOwnerUsername;
        var fulfilledByUserId = data.WaitingForUserId ?? data.CharacterAuthorId!.Value;

        // Notify the person who created the pendency
        // Don't notify if they fulfilled it themselves
        if (data.CreatedById == fulfilledByUserId)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { data.CreatedById },
            Metadata = new
            {
                PendencyId = data.PendencyId.EncodeToReadable(),
                GameId = data.GameId.EncodeToReadable(data.GameTitle),
                GameTitle = data.GameTitle,
                RoomId = data.RoomId.EncodeToReadable(),
                RoomTitle = data.RoomTitle,
                CharacterName = data.CharacterName,
                FulfilledByUsername = fulfilledByUsername
            }
        };
    }
}
