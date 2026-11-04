using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Subscriptions;

internal class NewForumTopicFromSubscribedAuthorNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public NewForumTopicFromSubscribedAuthorNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewTopic;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var topicData = await _dbContext.Topics
            .Where(t => t.TopicId == entityId)
            .Select(t => new
            {
                t.TopicId,
                t.Title,
                t.AuthorId,
                AuthorUsername = t.Author.Username,
                t.BoardId,
                BoardTitle = t.Board.Title
            })
            .FirstOrDefaultAsync();

        if (topicData == null)
        {
            yield break;
        }

        var subscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.User,
            topicData.AuthorId,
            SubscriptionSettings.AuthorTopicEvents);

        var subscriberIds = subscriptions
            .Select(s => s.SubscriberId)
            .ToArray();

        if (subscriberIds.Length == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            EventType = EventType.NewTopicFromSubscribedAuthor,
            UsersInterested = subscriberIds,
            ActorId = topicData.AuthorId,
            Metadata = new
            {
                TopicId = topicData.TopicId.EncodeToReadable(topicData.Title),
                TopicTitle = topicData.Title,
                BoardId = topicData.BoardId.EncodeToReadable(topicData.BoardTitle),
                topicData.BoardTitle,
                topicData.AuthorUsername
            }
        };
    }
}
