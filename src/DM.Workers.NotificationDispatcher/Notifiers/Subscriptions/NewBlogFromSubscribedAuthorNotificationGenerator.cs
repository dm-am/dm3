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

/// <summary>
/// Generates "new blog from subscribed author" notifications at blog creation, only for a
/// <see cref="DraftVisibility.Public"/> draft; the activation generator covers the others.
/// </summary>
internal class NewBlogFromSubscribedAuthorNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public NewBlogFromSubscribedAuthorNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewBlog;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var blogData = await _dbContext.Blogs
            .Where(b => b.BlogId == entityId)
            .Select(b => new
            {
                b.BlogId,
                b.Title,
                b.AuthorId,
                AuthorUsername = b.Author!.Username,
                b.DraftVisibility,
                AssistantIds = b.Assistants.Select(a => a.UserId).ToList()
            })
            .FirstOrDefaultAsync();

        if (blogData == null || blogData.DraftVisibility != DraftVisibility.Public)
        {
            yield break;
        }

        var usersInterested = await SubscribedAudience.OfTeamAsync(
            _subscriptionRepository,
            blogData.AuthorId,
            blogData.AssistantIds,
            SubscriptionSettings.AuthorBlogEvents);

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            EventType = EventType.NewBlogFromSubscribedAuthor,
            UsersInterested = usersInterested,
            ActorId = blogData.AuthorId,
            Metadata = new
            {
                BlogId = blogData.BlogId.EncodeToReadable(blogData.Title),
                BlogTitle = blogData.Title,
                blogData.AuthorUsername
            }
        };
    }
}
