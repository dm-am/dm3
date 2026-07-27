using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Messaging;

/// <summary>
/// Generates a realtime broadcast notification for new global chat messages
/// </summary>
/// <remarks>
/// Global chat is public, so the recipient set is "all connected users".
/// Connection state lives in the API process — the generator intentionally
/// leaves <see cref="CreateNotification.UsersInterested"/> empty (nothing
/// lands in anyone's stored notification list) and the API-side realtime
/// processor broadcasts this event type to every open SignalR connection.
/// </remarks>
internal class NewGlobalChatMessageNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public NewGlobalChatMessageNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewGlobalChatMessage;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var messageData = await _dbContext.Messages
            .Where(m => m.MessageId == entityId && !m.IsRemoved)
            .Select(m => new
            {
                m.MessageId,
                m.ChatId,
                AuthorUserId = m.UserId,
                AuthorUsername = m.Author.Username,
                m.CreatedUtc
            })
            .FirstOrDefaultAsync();

        if (messageData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            // Empty on purpose: fan-out to all connected clients happens
            // in the API realtime processor, not through per-user stored
            // notifications (which would spam every user's inbox)
            UsersInterested = [],
            Metadata = new
            {
                messageData.MessageId,
                messageData.ChatId,
                messageData.AuthorUserId,
                messageData.AuthorUsername,
                messageData.CreatedUtc
            }
        };
    }
}
