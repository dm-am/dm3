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
/// The entity is the comment, not the ticket: one ticket is answered as many times
/// as the conversation takes, and each answer is news of its own. The answer passes
/// the turn to the author, and he is told so only when he has an account: a guest
/// has no inbox, and his tracking page says the answer will appear there.
///
/// The comment is checked to be somebody else's than the author's. The service
/// raises this event only for a moderator who is not the author, and a moderator
/// who filed a ticket writes in it as its author; telling a person about his own
/// comment would be the one thing worse than telling him nothing.
/// </remarks>
internal class TicketAnsweredNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
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

        // A guest's ticket tells nobody, and a comment by nobody (the guest's) or
        // by the author himself is not an answer to him.
        if (answer?.TicketAuthorId == null || answer.AuthorId == null ||
            answer.AuthorId == answer.TicketAuthorId)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { answer.TicketAuthorId.Value },
            // No ActorId although the moderator who answered is on the row: an
            // answer to a ticket is not less delivered because its author has the
            // moderator on his blacklist, as with every moderation decision.
            Metadata = new
            {
                // The page of the ticket is built out of this field, in the list
                // and in the letter alike.
                TicketId = answer.TicketId,
                ModeratorUsername = answer.ModeratorUsername,
                CreatedUtc = answer.CreatedUtc
            }
        };
    }
}
