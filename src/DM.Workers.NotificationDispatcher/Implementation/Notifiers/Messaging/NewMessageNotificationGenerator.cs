using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Messaging;

/// <summary>
/// Tells the participants of a chat that it has a new message
/// </summary>
/// <remarks>
/// Delivery is realtime only. The unread counter is already incremented by the
/// messaging domain, so nothing has to be stored here: the push exists to make
/// an open tab re-read that counter. A stored notification would put every
/// private message into the notification list and into the mail queue, which is
/// a decision about what to tell the user, not a way to refresh a badge.
/// Recipients come from the participant rows of the chat, so the two chats that
/// have none produce nothing at all: global chat has its own broadcast event,
/// and a game room chat is entered through the room rather than the messenger.
/// </remarks>
internal class NewMessageNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
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
            // Neither the text nor the author travels with the push: the client
            // answers it by re-reading its chat list from the API, which applies
            // the reader's own access rules to whatever it returns.
            Metadata = new
            {
                messageData.MessageId,
                messageData.ChatId
            }
        };
    }
}
