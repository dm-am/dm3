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
/// Generates notifications when a new post is created in a game that users are subscribed to (as readers)
/// </summary>
internal class NewPostInSubscribedGameNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    /// <inheritdoc />
    public NewPostInSubscribedGameNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewPost;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var postData = await _dbContext.Posts
            .Where(p => p.PostId == entityId)
            .Select(p => new
            {
                p.PostId,
                p.Room!.GameId,
                GameTitle = p.Room.Game!.Title,
                p.Room.RoomId,
                RoomTitle = p.Room.Title,
                p.UserId,
                AuthorLogin = p.Author!.Login,
                CharacterName = p.CharacterId.HasValue ? p.Character!.Name : null,
                p.Room.Game.MasterId,
                p.Room.Game.AssistantId
            })
            .FirstOrDefaultAsync();

        if (postData == null)
        {
            yield break;
        }

        // Get all subscriptions to this game with NewPosts setting
        var subscriptions = await _subscriptionRepository.GetByTargetWithSettings(
            SubscriptionTargetType.Game,
            postData.GameId,
            SubscriptionSettings.NewPosts);

        // Exclude the post author and game staff (master, assistant) who see posts anyway
        var subscriberIds = subscriptions
            .Where(s => s.SubscriberId != postData.UserId)
            .Where(s => s.SubscriberId != postData.MasterId)
            .Where(s => postData.AssistantId == null || s.SubscriberId != postData.AssistantId)
            .Select(s => s.SubscriberId)
            .ToArray();

        if (subscriberIds.Length == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            EventType = EventType.NewPostInSubscribedGame,
            UsersInterested = subscriberIds,
            Metadata = new
            {
                PostId = postData.PostId,
                GameId = postData.GameId.EncodeToReadable(postData.GameTitle),
                GameTitle = postData.GameTitle,
                RoomId = postData.RoomId,
                RoomTitle = postData.RoomTitle,
                AuthorLogin = postData.AuthorLogin,
                CharacterName = postData.CharacterName
            }
        };
    }
}
