using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Blog;

/// <summary>
/// Generates notifications when a publication is updated/changed.
/// Notifies blog readers (subscribers).
/// </summary>
internal class ChangedPublicationNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    /// <inheritdoc />
    public ChangedPublicationNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.ChangedPublication;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var publicationData = await _dbContext.Publications
            .Where(p => p.PublicationId == entityId)
            .Select(p => new
            {
                p.PublicationId,
                p.Title,
                p.AuthorId,
                AuthorUsername = p.Author.Username,
                p.BlogId,
                BlogTitle = p.Blog.Title,
                p.IsPublished
            })
            .FirstOrDefaultAsync();

        if (publicationData == null || !publicationData.IsPublished)
        {
            yield break;
        }

        // Find all users subscribed to this blog
        var subscriptions = await _subscriptionRepository.GetByTargetWithSettings(
            SubscriptionTargetType.Blog,
            publicationData.BlogId,
            SubscriptionSettings.NewPublications); // Same setting as new publications

        var subscriberIds = subscriptions
            .Where(s => s.SubscriberId != publicationData.AuthorId)
            .Select(s => s.SubscriberId)
            .ToArray();

        if (subscriberIds.Length == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = subscriberIds,
            Metadata = new
            {
                PublicationId = publicationData.PublicationId.EncodeToReadable(publicationData.Title),
                PublicationTitle = publicationData.Title,
                BlogId = publicationData.BlogId.EncodeToReadable(publicationData.BlogTitle),
                BlogTitle = publicationData.BlogTitle,
                AuthorUsername = publicationData.AuthorUsername
            }
        };
    }
}
