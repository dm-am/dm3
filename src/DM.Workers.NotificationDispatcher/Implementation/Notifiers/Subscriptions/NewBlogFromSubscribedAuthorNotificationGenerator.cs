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
/// Generates "new blog from subscribed author" notifications at blog-creation
/// time — but only for blogs whose draft visibility is <see cref="DraftVisibility.Public"/>.
/// Private drafts are invisible to non-team users; the activation generator
/// covers them later when the blog becomes Active.
/// </summary>
internal class NewBlogFromSubscribedAuthorNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    /// <inheritdoc />
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

        var usersInterested = new HashSet<Guid>();

        var authorSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.User,
            blogData.AuthorId,
            SubscriptionSettings.AuthorBlogEvents);
        usersInterested.UnionWith(authorSubscriptions.Select(s => s.SubscriberId));

        foreach (var assistantId in blogData.AssistantIds)
        {
            var assistantSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
                SubscriptionTargetType.User,
                assistantId,
                SubscriptionSettings.AuthorBlogEvents);
            usersInterested.UnionWith(assistantSubscriptions.Select(s => s.SubscriberId));
        }

        // Exclude the team — they manage the blog themselves.
        usersInterested.Remove(blogData.AuthorId);
        foreach (var assistantId in blogData.AssistantIds)
        {
            usersInterested.Remove(assistantId);
        }

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            EventType = EventType.NewBlogFromSubscribedAuthor,
            UsersInterested = usersInterested.ToArray(),
            ActorId = blogData.AuthorId,
            Metadata = new
            {
                BlogId = blogData.BlogId.EncodeToReadable(blogData.Title),
                BlogTitle = blogData.Title,
                AuthorUsername = blogData.AuthorUsername
            }
        };
    }
}
