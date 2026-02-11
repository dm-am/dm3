using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Community.BusinessProcesses.Subscriptions;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.Notifications.Dto;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Notifications.Consumer.Implementation.Notifiers.Subscriptions;

/// <summary>
/// Generates notifications when a new topic is created in a board that users are subscribed to
/// </summary>
internal class NewTopicInSubscribedBoardNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    /// <inheritdoc />
    public NewTopicInSubscribedBoardNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewForumTopic;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var topicData = await _dbContext.Topics
            .Where(t => t.TopicId == entityId)
            .Select(t => new
            {
                t.TopicId,
                t.Title,
                t.BoardId,
                BoardTitle = t.Board.Title,
                t.UserId,
                AuthorLogin = t.Author.Login
            })
            .FirstOrDefaultAsync();

        if (topicData == null)
        {
            yield break;
        }

        var subscriptions = await _subscriptionRepository.GetByTargetWithSettings(
            SubscriptionTargetType.Board,
            topicData.BoardId,
            SubscriptionSettings.NewTopics);

        var subscriberIds = subscriptions
            .Where(s => s.SubscriberId != topicData.UserId)
            .Select(s => s.SubscriberId)
            .ToArray();

        if (subscriberIds.Length == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            EventType = EventType.NewTopicInSubscribedBoard,
            UsersInterested = subscriberIds,
            Metadata = new
            {
                TopicId = topicData.TopicId.EncodeToReadable(topicData.Title),
                TopicTitle = topicData.Title,
                BoardId = topicData.BoardId,
                BoardTitle = topicData.BoardTitle,
                AuthorLogin = topicData.AuthorLogin
            }
        };
    }
}
