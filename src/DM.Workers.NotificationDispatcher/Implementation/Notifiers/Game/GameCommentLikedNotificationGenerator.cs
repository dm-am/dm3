using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Game;

/// <summary>
/// Generates notifications when a game comment is liked
/// </summary>
internal class GameCommentLikedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GameCommentLikedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.LikedGameComment;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var likedCommentData = await (
            from like in _dbContext.Likes
            join comment in _dbContext.Comments on like.EntityId equals comment.CommentId
            join game in _dbContext.Games on comment.EntityId equals game.GameId
            where like.LikeId == entityId && like.EntityType == LikeEntityType.Comment
            select new
            {
                LikerId = like.UserId,
                LikerUsername = like.User!.Username,
                comment.CommentId,
                comment.AuthorId,
                game.GameId,
                GameTitle = game.Title
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
                GameId = likedCommentData.GameId.EncodeToReadable(likedCommentData.GameTitle),
                GameTitle = likedCommentData.GameTitle
            }
        };
    }
}
