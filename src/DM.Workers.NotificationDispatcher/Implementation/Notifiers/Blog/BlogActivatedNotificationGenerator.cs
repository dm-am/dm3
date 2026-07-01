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
/// blog author + assistants (with <see cref="SubscriptionSettings.AuthorBlogEvents"/>)
/// and direct blog readers (with <see cref="SubscriptionSettings.StatusChanges"/>).
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

        var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.Blog,
            blogData.BlogId,
            SubscriptionSettings.StatusChanges);
        usersInterested.UnionWith(readerSubscriptions.Select(s => s.SubscriberId));

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

        yield return new CreateNotification
        {
            EventType = EventType.NewBlogFromSubscribedAuthor,
            UsersInterested = usersInterested.ToArray(),
            Metadata = new
            {
                BlogId = blogData.BlogId.EncodeToReadable(blogData.Title),
                BlogTitle = blogData.Title,
                AuthorUsername = blogData.AuthorUsername
            }
        };
    }
}
