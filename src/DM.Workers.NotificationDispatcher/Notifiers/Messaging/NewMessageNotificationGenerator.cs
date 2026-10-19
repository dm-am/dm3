using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Messaging;

/// <summary>
/// Tells the participants of a chat that it has a new message
/// </summary>
/// <remarks>
/// Realtime only: the messaging domain keeps the unread counter, the push makes an open tab re-read it.
/// </remarks>
internal class NewMessageNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public NewMessageNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewMessage;

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
                RecipientIds = m.Chat.UserLinks
                    .Where(link => link.UserId != m.UserId)
                    .Select(link => link.UserId)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (messageData == null || messageData.RecipientIds.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            RealtimeOnly = true,
            UsersInterested = messageData.RecipientIds,
            ActorId = messageData.AuthorUserId,
            // The client re-reads its chat list, which applies its own access rules.
            Metadata = new
            {
                messageData.MessageId,
                messageData.ChatId
            }
        };
    }
}
