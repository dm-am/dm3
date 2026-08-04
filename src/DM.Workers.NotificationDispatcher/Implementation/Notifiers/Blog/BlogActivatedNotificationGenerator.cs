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
/// Notification generator when a blog transitions to Active status. Mirrors
/// <c>GameActivatedNotificationGenerator</c>: notifies subscribers of the
/// blog author + assistants (with <see cref="SubscriptionSettings.AuthorBlogEvents"/>).
/// </summary>
internal class BlogActivatedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    /// <inheritdoc />
    public BlogActivatedNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.StatusBlogActive;

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
                AssistantIds = b.Assistants.Select(a => a.UserId).ToList()
            })
            .FirstOrDefaultAsync();

        if (blogData == null)
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

        // Readers of this blog are deliberately absent. They are subscribed to the
        // blog itself, so BlogStatusChangedNotificationGenerator answers the same
        // event and already tells them it went active under the event type of the
        // activation. This notification renames the event to
        // NewBlogFromSubscribedAuthor, which is only true for the audience that
        // could not see the blog while it was a draft; delivered to a reader it was
        // a second copy of one activation calling a blog they already follow new.

        // Exclude the team — they're notified through team-targeted generators.
        usersInterested.Remove(blogData.AuthorId);
        foreach (var assistantId in blogData.AssistantIds)
        {
            usersInterested.Remove(assistantId);
        }

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        // The author is the actor: the recipients subscribed to him, not to the blog,
        // and the notification reports his blog opening under his name. Whoever of the
        // team flipped the switch, a subscriber who blocked the author is not told
        // about a new blog of his.
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
