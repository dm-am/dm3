using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Blog;

/// <summary>
/// Generates notifications when a publication is updated/changed.
/// Notifies blog readers (subscribers).
/// </summary>
internal class ChangedPublicationNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

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
                p.ModifiedByUserId,
                AuthorUsername = p.Author.Username,
                p.BlogId,
                BlogTitle = p.Blog.Title
            })
            .FirstOrDefaultAsync();

        if (publicationData == null)
        {
            yield break;
        }

        var subscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
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

        // Not filled from AuthorId: an assistant or an admin may be the editor.
        yield return new CreateNotification
        {
            UsersInterested = subscriberIds,
            ActorId = publicationData.ModifiedByUserId,
            Metadata = new
            {
                PublicationId = publicationData.PublicationId.EncodeToReadable(publicationData.Title),
                PublicationTitle = publicationData.Title,
                BlogId = publicationData.BlogId.EncodeToReadable(publicationData.BlogTitle),
                publicationData.BlogTitle,
                publicationData.AuthorUsername
            }
        };
    }
}
