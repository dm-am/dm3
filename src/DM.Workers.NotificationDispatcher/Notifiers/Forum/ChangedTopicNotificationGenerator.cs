using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Forum;

/// <summary>
/// Generates notifications when a forum topic is updated/changed.
/// Notifies topic subscribers.
/// </summary>
internal class ChangedTopicNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public ChangedTopicNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.ChangedTopic;

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
            SubscriptionTargetType.Topic,
            topicData.TopicId,
            SubscriptionSettings.NewComments);

        var subscriberIds = subscriptions
            .Where(s => s.SubscriberId != topicData.AuthorId) // Don't notify author about their own changes
            .Select(s => s.SubscriberId)
            .ToArray();

        if (subscriberIds.Length == 0)
        {
            yield break;
        }

        // Not filled from AuthorId: a moderator or an administrator may be the editor.
        var lastEditorId = await _dbContext.TopicEdits
            .Where(e => e.TopicId == topicData.TopicId)
            .OrderByDescending(e => e.EditedUtc)
            .Select(e => (Guid?)e.EditorUserId)
            .FirstOrDefaultAsync();

        yield return new CreateNotification
        {
            UsersInterested = subscriberIds,
            ActorId = lastEditorId,
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
