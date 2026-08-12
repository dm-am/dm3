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
/// Notification generator for blog status changes. Mirrors
/// <c>GameStatusChangedNotificationGenerator</c>:
/// notifies the author and assistants (always) and blog readers
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

    /// <inheritdoc />
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
        var data = await _dbContext.Blogs
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

        if (data == null)
        {
            yield break;
        }

        // Get subscribers (readers) who have StatusChanges enabled
        var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.Blog,
            entityId,
            SubscriptionSettings.StatusChanges);
        var readerIds = readerSubscriptions.Select(s => s.SubscriberId);

        // Combine all recipients: Author, Assistants (always), Readers (with StatusChanges)
        var usersInterested = new HashSet<Guid> { data.AuthorId };
        usersInterested.UnionWith(data.AssistantIds);
        usersInterested.UnionWith(readerIds);

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        // ActorId stays null: the blog keeps no record of who moved the status. The
        // transition is made by the author, an assistant or a moderator through
        // BlogService.ChangeStatusAsync, and none of them is written to the row, so
        // there is nothing here to filter blacklisted recipients on. AuthorId is not
        // it: he is a recipient of this one, not necessarily its cause.
        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            Metadata = new
            {
                BlogTitle = data.Title,
                BlogId = data.BlogId.EncodeToReadable(data.Title),
                NewStatus = data.Status.ToString(),
                AuthorUsername = data.AuthorUsername
            }
        };
    }
}
