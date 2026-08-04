using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Forum;

/// <summary>
/// Generates notifications when a topic comment is liked
/// </summary>
internal class TopicCommentLikedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public TopicCommentLikedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.LikedTopicComment;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var likedCommentData = await (
            from like in _dbContext.Likes
            join comment in _dbContext.Comments on like.EntityId equals comment.CommentId
            join topic in _dbContext.Topics on comment.EntityId equals topic.TopicId
            where like.LikeId == entityId && like.EntityType == LikeEntityType.Comment
            select new
            {
                LikerUsername = like.User!.Username,
                LikerId = like.UserId,
                comment.CommentId,
                comment.AuthorId,
                TopicId = topic.TopicId,
                TopicTitle = topic.Title
            })
            .FirstOrDefaultAsync();

        if (likedCommentData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [likedCommentData.AuthorId],
            ActorId = likedCommentData.LikerId,
            Metadata = new
            {
                LikerUsername = likedCommentData.LikerUsername,
                TopicId = likedCommentData.TopicId.EncodeToReadable(likedCommentData.TopicTitle),
                TopicTitle = likedCommentData.TopicTitle
            }
        };
    }
}
