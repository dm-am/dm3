using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Blog;

/// <summary>
/// Generates notifications when a blog comment is liked
/// </summary>
internal class BlogCommentLikedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public BlogCommentLikedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.LikedBlogComment;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var likedCommentData = await (
            from like in _dbContext.Likes
            join comment in _dbContext.Comments on like.EntityId equals comment.CommentId
            join blog in _dbContext.Blogs on comment.EntityId equals blog.BlogId
            where like.LikeId == entityId && like.EntityType == LikeEntityType.Comment
            select new
            {
                LikerUsername = like.User!.Username,
                comment.CommentId,
                comment.AuthorId,
                blog.BlogId,
                BlogTitle = blog.Title
            })
            .FirstOrDefaultAsync();

        if (likedCommentData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [likedCommentData.AuthorId],
            Metadata = new
            {
                LikerUsername = likedCommentData.LikerUsername,
                BlogId = likedCommentData.BlogId.EncodeToReadable(likedCommentData.BlogTitle),
                BlogTitle = likedCommentData.BlogTitle
            }
        };
    }
}
