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
/// Notification generator when a blog transitions to Active status: subscribers of the
/// blog author and assistants (with <see cref="SubscriptionSettings.AuthorBlogEvents"/>).
/// </summary>
internal class BlogActivatedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

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

        // No readers: BlogStatusChangedNotificationGenerator already tells them about the activation.
        var usersInterested = await SubscribedAudience.OfTeamAsync(
            _subscriptionRepository,
            blogData.AuthorId,
            blogData.AssistantIds,
            SubscriptionSettings.AuthorBlogEvents);

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        // The author is the actor: the recipients subscribed to him.
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
