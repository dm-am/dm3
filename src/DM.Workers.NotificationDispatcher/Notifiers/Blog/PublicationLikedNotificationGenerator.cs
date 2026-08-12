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
/// Generates notifications when a blog publication is liked
/// </summary>
internal class PublicationLikedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public PublicationLikedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.LikedPublication;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var likedPublicationData = await (
            from like in _dbContext.Likes
            join publication in _dbContext.Publications on like.EntityId equals publication.PublicationId
            where like.LikeId == entityId && like.EntityType == LikeEntityType.Publication
            select new
            {
                LikerId = like.UserId,
                LikerUsername = like.User!.Username,
                publication.PublicationId,
                publication.AuthorId,
                publication.Title,
                publication.BlogId,
                BlogTitle = publication.Blog.Title
            })
            .FirstOrDefaultAsync();

        if (likedPublicationData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [likedPublicationData.AuthorId],
            ActorId = likedPublicationData.LikerId,
            Metadata = new
            {
                LikerUsername = likedPublicationData.LikerUsername,
                PublicationId = likedPublicationData.PublicationId.EncodeToReadable(likedPublicationData.Title),
                PublicationTitle = likedPublicationData.Title,
                BlogId = likedPublicationData.BlogId.EncodeToReadable(likedPublicationData.BlogTitle),
                BlogTitle = likedPublicationData.BlogTitle
            }
        };
    }
}
