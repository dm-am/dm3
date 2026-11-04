using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Messaging;

/// <summary>
/// Generates a realtime broadcast notification for new global chat messages
/// </summary>
/// <remarks>
/// UsersInterested is empty: the API broadcasts to every connection.
/// </remarks>
internal class NewGlobalChatMessageNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

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
            // Empty: the API broadcasts to every connection
            UsersInterested = [],
            // Who wrote the message, although there is no recipient list to filter
            ActorId = messageData.AuthorUserId,
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
