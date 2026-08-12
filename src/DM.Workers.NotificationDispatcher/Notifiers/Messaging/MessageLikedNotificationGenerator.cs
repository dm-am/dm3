using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Messaging;

/// <summary>
/// Generates notifications when a message is liked
/// </summary>
internal class MessageLikedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public MessageLikedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.LikedMessage;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var likedMessageData = await (
            from like in _dbContext.Likes
            join message in _dbContext.Messages on like.EntityId equals message.MessageId
            where like.LikeId == entityId && like.EntityType == LikeEntityType.Message
            select new
            {
                LikerId = like.UserId,
                LikerUsername = like.User!.Username,
                message.MessageId,
                message.UserId,
                message.ChatId
            })
            .FirstOrDefaultAsync();

        if (likedMessageData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [likedMessageData.UserId],
            ActorId = likedMessageData.LikerId,
            Metadata = new
            {
                LikerUsername = likedMessageData.LikerUsername,
                ChatId = likedMessageData.ChatId
            }
        };
    }
}
