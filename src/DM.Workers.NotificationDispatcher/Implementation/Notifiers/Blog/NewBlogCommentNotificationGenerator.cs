using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Blog;

/// <summary>
/// Generates notifications when a new comment is added to a blog.
/// Notifies blog owner and assistants.
/// </summary>
internal class NewBlogCommentNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public NewBlogCommentNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewBlogComment;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        // Comments are polymorphic - EntityId is the BlogId for blog comments
        var commentData = await (
            from c in _dbContext.Comments
            join b in _dbContext.Blogs on c.EntityId equals b.BlogId
            where c.CommentId == entityId && !c.IsRemoved && !b.IsRemoved
            select new
            {
                c.CommentId,
                c.AuthorId,
                CommentAuthorUsername = c.Author.Username,
                BlogId = b.BlogId,
                BlogTitle = b.Title,
                BlogAuthorId = b.AuthorId,
                BlogMentorId = b.MentorId,
                Assistants = b.Assistants
                    .Select(a => a.UserId)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (commentData == null)
        {
            yield break;
        }

        // Collect all users to notify (owner, mentor, assistants)
        var usersToNotify = new HashSet<Guid> { commentData.BlogAuthorId };

        if (commentData.BlogMentorId.HasValue)
        {
            usersToNotify.Add(commentData.BlogMentorId.Value);
        }

        foreach (var assistantId in commentData.Assistants)
        {
            usersToNotify.Add(assistantId);
        }

        // Don't notify the comment author
        usersToNotify.Remove(commentData.AuthorId);

        if (usersToNotify.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = usersToNotify.ToArray(),
            ActorId = commentData.AuthorId,
            Metadata = new
            {
                CommentId = commentData.CommentId,
                BlogId = commentData.BlogId.EncodeToReadable(commentData.BlogTitle),
                BlogTitle = commentData.BlogTitle,
                AuthorUsername = commentData.CommentAuthorUsername
            }
        };
    }
}
