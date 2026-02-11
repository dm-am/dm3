using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.Notifications.Dto;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Notifications.Consumer.Implementation.Notifiers.Forum;

/// <inheritdoc />
internal class TopicLikedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
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
                like.User!.Login,
                topic.TopicId,
                topic.UserId,
                topic.Title
            })
            .FirstAsync();

        yield return new CreateNotification
        {
            UsersInterested = new[] {likedTopicData.UserId},
            Metadata = new
            {
                AuthorLogin = likedTopicData.Login,
                TopicTitle = likedTopicData.Title,
                TopicId = likedTopicData.TopicId.EncodeToReadable(likedTopicData.Title)
            }
        };
    }
}