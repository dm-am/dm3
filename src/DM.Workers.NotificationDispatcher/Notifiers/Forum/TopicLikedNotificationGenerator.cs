using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Forum;

/// <inheritdoc />
internal class TopicLikedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public TopicLikedNotificationGenerator(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.LikedTopic;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var likedTopicData = await (
            from like in _dbContext.Likes
            join topic in _dbContext.Topics on like.EntityId equals topic.TopicId
            where like.LikeId == entityId && like.EntityType == LikeEntityType.Topic
            select new
            {
                like.User!.Username,
                LikerId = like.UserId,
                topic.TopicId,
                topic.AuthorId,
                topic.Title
            })
            .FirstOrDefaultAsync();

        // Not stored, so a redelivery after the like or the topic has gone answers with nothing.
        if (likedTopicData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [likedTopicData.AuthorId],
            ActorId = likedTopicData.LikerId,
            Metadata = new
            {
                AuthorUsername = likedTopicData.Username,
                TopicTitle = likedTopicData.Title,
                TopicId = likedTopicData.TopicId.EncodeToReadable(likedTopicData.Title)
            }
        };
    }
}
