using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Blog;

/// <summary>
/// Generates notifications when a publication is commented on
/// </summary>
/// <remarks>
/// A comment carries the id of whatever it hangs on in EntityId, so the join is
/// what tells the two kinds of blog comment apart: this one lands on a
/// publication, the blog-level one on the blog itself. Publishing the blog event
/// for a publication comment therefore did not fail loudly — it reached a
/// generator that joined Comments to Blogs, matched nothing, and yielded no
/// notification at all.
/// </remarks>
internal class NewPublicationCommentNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public NewPublicationCommentNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewPublicationComment;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var commentData = await (
            from c in _dbContext.Comments
            join p in _dbContext.Publications on c.EntityId equals p.PublicationId
            where c.CommentId == entityId && !c.IsRemoved && !p.IsRemoved && !p.Blog.IsRemoved
            select new
            {
                c.CommentId,
                c.AuthorId,
                CommentAuthorUsername = c.Author.Username,
                p.PublicationId,
                PublicationTitle = p.Title,
                PublicationAuthorId = p.AuthorId,
                p.BlogId,
                BlogTitle = p.Blog.Title,
                BlogAuthorId = p.Blog.AuthorId,
                BlogMentorId = p.Blog.MentorId,
                Assistants = p.Blog.Assistants
                    .Select(a => a.UserId)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (commentData == null)
        {
            yield break;
        }

        // The publication's own author first — on a blog with assistants they are
        // often not the blog owner, and a comment on their text is theirs to hear
        // about. The rest of the staff the blog-level generator notifies.
        var usersToNotify = new HashSet<Guid>
        {
            commentData.PublicationAuthorId,
            commentData.BlogAuthorId
        };

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
                PublicationId = commentData.PublicationId.EncodeToReadable(commentData.PublicationTitle),
                PublicationTitle = commentData.PublicationTitle,
                BlogId = commentData.BlogId.EncodeToReadable(commentData.BlogTitle),
                BlogTitle = commentData.BlogTitle,
                AuthorUsername = commentData.CommentAuthorUsername
            }
        };
    }
}
