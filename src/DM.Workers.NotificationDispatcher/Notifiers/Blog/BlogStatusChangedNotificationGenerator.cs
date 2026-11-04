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
/// Notification generator for blog status changes: the author and assistants (always) and blog readers
/// (if <see cref="SubscriptionSettings.StatusChanges"/> is enabled).
/// </summary>
internal class BlogStatusChangedNotificationGenerator : INotificationGenerator
{
    private static readonly EventType[] SupportedTypes =
    {
        EventType.StatusBlogActive,
        EventType.StatusBlogClosed,
        EventType.StatusBlogFrozen,
        EventType.StatusBlogFinished
    };

    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public BlogStatusChangedNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    public bool CanResolve(EventType eventType) => SupportedTypes.Contains(eventType);

    /// <inheritdoc />
    public async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var blogData = await _dbContext.Blogs
            .Where(b => b.BlogId == entityId)
            .Select(b => new
            {
                b.BlogId,
                b.Title,
                b.Status,
                b.AuthorId,
                AuthorUsername = b.Author!.Username,
                AssistantIds = b.Assistants.Select(a => a.UserId).ToList()
            })
            .FirstOrDefaultAsync();

        if (blogData == null)
        {
            yield break;
        }

        var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.Blog,
            entityId,
            SubscriptionSettings.StatusChanges);
        var readerIds = readerSubscriptions.Select(s => s.SubscriberId);

        var usersInterested = new HashSet<Guid> { blogData.AuthorId };
        usersInterested.UnionWith(blogData.AssistantIds);
        usersInterested.UnionWith(readerIds);

        // No ActorId: the blog does not record who moved the status.
        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            Metadata = new
            {
                BlogTitle = blogData.Title,
                BlogId = blogData.BlogId.EncodeToReadable(blogData.Title),
                NewStatus = blogData.Status.ToString(),
                blogData.AuthorUsername
            }
        };
    }
}
