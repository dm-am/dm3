using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Subscriptions;

/// <summary>
/// Generates notifications when a new comment is added to a topic that users are subscribed to
/// </summary>
internal class NewCommentInSubscribedTopicNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    /// <inheritdoc />
    public NewCommentInSubscribedTopicNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewTopicComment;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var commentData = await _dbContext.Comments
            .Where(c => c.CommentId == entityId && c.Topic != null)
            .Select(c => new
            {
                c.CommentId,
                TopicId = c.EntityId,
                TopicTitle = c.Topic!.Title,
                c.AuthorId,
                AuthorUsername = c.Author!.Username,
                TopicAuthorId = c.Topic.AuthorId
            })
            .FirstOrDefaultAsync();

        if (commentData == null)
        {
            yield break;
        }

        var subscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.Topic,
            commentData.TopicId,
            SubscriptionSettings.NewComments);

        // Notify subscribers except the comment author
        // Also notify topic author even if not explicitly subscribed (via topic author subscription)
        var subscriberIds = subscriptions
            .Where(s => s.SubscriberId != commentData.AuthorId)
            .Select(s => s.SubscriberId)
            .ToList();

        // Add topic author if they're not the commenter and not already subscribed
        if (commentData.TopicAuthorId != commentData.AuthorId &&
            !subscriberIds.Contains(commentData.TopicAuthorId))
        {
            subscriberIds.Add(commentData.TopicAuthorId);
        }

        if (subscriberIds.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            EventType = EventType.NewCommentInSubscribedTopic,
            UsersInterested = subscriberIds,
            Metadata = new
            {
                CommentId = commentData.CommentId,
                TopicId = commentData.TopicId.EncodeToReadable(commentData.TopicTitle),
                TopicTitle = commentData.TopicTitle,
                AuthorUsername = commentData.AuthorUsername
            }
        };
    }
}
