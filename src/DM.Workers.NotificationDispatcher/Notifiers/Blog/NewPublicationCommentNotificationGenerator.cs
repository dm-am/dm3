using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Blog;

/// <remarks>
/// The join tells the two kinds of blog comment apart: this one hangs on a publication.
/// </remarks>
internal class NewPublicationCommentNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

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

        // The publication author may not be the blog owner; the blog-level generator tells the rest of the staff.
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

        usersToNotify.Remove(commentData.AuthorId);

        if (usersToNotify.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = usersToNotify,
            ActorId = commentData.AuthorId,
            Metadata = new
            {
                commentData.CommentId,
                PublicationId = commentData.PublicationId.EncodeToReadable(commentData.PublicationTitle),
                commentData.PublicationTitle,
                BlogId = commentData.BlogId.EncodeToReadable(commentData.BlogTitle),
                commentData.BlogTitle,
                AuthorUsername = commentData.CommentAuthorUsername
            }
        };
    }
}
