using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Moderation;

/// <summary>
/// Generates notification when a moderator writes in the conversation of a ticket.
/// Notifies the author of the ticket.
/// </summary>
/// <remarks>
/// The entity is the comment: each answer is news of its own. A guest has no inbox.
/// </remarks>
internal class TicketAnsweredNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public TicketAnsweredNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.TicketAnswered;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var answer = await _dbContext.TicketComments
            .Where(c => c.TicketCommentId == entityId)
            .Select(c => new
            {
                c.TicketId,
                c.AuthorId,
                ModeratorUsername = c.Author != null ? c.Author.Username : null,
                TicketAuthorId = c.Ticket.UserId,
                c.CreatedUtc
            })
            .FirstOrDefaultAsync();

        // A guest's ticket tells nobody, and the author's own comment is not an answer to him.
        if (answer?.TicketAuthorId == null || answer.AuthorId == null ||
            answer.AuthorId == answer.TicketAuthorId)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [answer.TicketAuthorId.Value],
            // No ActorId: a moderation decision is delivered whoever is on the blacklist.
            Metadata = new
            {
                // The link to the ticket is built out of this field.
                answer.TicketId,
                answer.ModeratorUsername,
                answer.CreatedUtc
            }
        };
    }
}
