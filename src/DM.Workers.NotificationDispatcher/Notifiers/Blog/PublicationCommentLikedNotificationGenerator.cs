using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Blog;

/// <summary>
/// Generates notifications when a publication comment is liked
/// </summary>
internal class PublicationCommentLikedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public PublicationCommentLikedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.LikedPublicationComment;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var likedCommentData = await (
            from like in _dbContext.Likes
            join comment in _dbContext.Comments on like.EntityId equals comment.CommentId
            join publication in _dbContext.Publications on comment.EntityId equals publication.PublicationId
            where like.LikeId == entityId && like.EntityType == LikeEntityType.Comment
            select new
            {
                LikerId = like.UserId,
                LikerUsername = like.User!.Username,
                comment.CommentId,
                comment.AuthorId,
                publication.PublicationId,
                PublicationTitle = publication.Title,
                publication.BlogId,
                BlogTitle = publication.Blog.Title
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
                PublicationId = likedCommentData.PublicationId.EncodeToReadable(likedCommentData.PublicationTitle),
                PublicationTitle = likedCommentData.PublicationTitle,
                BlogId = likedCommentData.BlogId.EncodeToReadable(likedCommentData.BlogTitle),
                BlogTitle = likedCommentData.BlogTitle
            }
        };
    }
}
