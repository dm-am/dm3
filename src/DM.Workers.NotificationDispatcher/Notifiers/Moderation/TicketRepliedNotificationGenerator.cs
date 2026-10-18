using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Tickets;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Moderation;

/// <summary>
/// Generates notification when the author of a ticket writes in its conversation,
/// from his account or as the guest holding its tracking token.
/// Notifies the moderators who have written in it.
/// </summary>
/// <remarks>
/// The entity is the comment. The reply passes the turn back to moderation, and the
/// moderators it is news to are the ones already in the conversation: whoever has
/// not written in it learns of the ticket from the queue, which is where everybody
/// else on duty looks, and is not called to every ticket a colleague is settling.
///
/// Of those who wrote, the ones the ticket is still moderation's business of right
/// now, by the rule the ticket service and the queue read (TicketVisibility): a
/// moderator who lost the rank since, or whose role no longer reads the category,
/// cannot open the page the notification leads to. The violator a complaint names
/// never gets past the service into the conversation, and the rule keeps him out of
/// here too, whatever put him among the writers. The author is left out: a
/// moderator who filed a ticket writes in it as its author, and his reply is not
/// news to himself.
/// </remarks>
internal class TicketRepliedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public TicketRepliedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.TicketReplied;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var reply = await _dbContext.TicketComments
            .Where(c => c.TicketCommentId == entityId)
            .Select(c => new
            {
                c.TicketId,
                // Null for the guest, who has no name to give
                AuthorUsername = c.Author != null ? c.Author.Username : null,
                TicketAuthorId = c.Ticket.UserId,
                c.Ticket.Subtype,
                c.Ticket.TargetId,
                c.CreatedUtc
            })
            .FirstOrDefaultAsync();

        if (reply == null)
        {
            yield break;
        }

        // The writers of this conversation who are still on the staff. System is
        // excluded because the robot account cannot read, and a removed account is
        // out of the read altogether.
        var writers = await _dbContext.Users
            .Where(u => !u.IsRemoved)
            .Where(u => u.Role >= UserRole.Moderator && u.Role < UserRole.System)
            .Where(u => _dbContext.TicketComments.Any(c => c.TicketId == reply.TicketId && c.AuthorId == u.UserId))
            .Select(u => new { u.UserId, u.Role })
            .ToArrayAsync();

        var moderatorIds = writers
            .Where(u => u.UserId != reply.TicketAuthorId)
            .Where(u => TicketVisibility.Moderates(u.UserId, u.Role, reply.Subtype, reply.TargetId))
            .Select(u => u.UserId)
            .ToArray();

        if (moderatorIds.Length == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = moderatorIds,
            // No ActorId although the author is on the row: a reply reaching the
            // moderators settling a ticket cannot depend on whether one of them
            // blacklisted its author, as with the filing of it.
            Metadata = new
            {
                // The moderation page of the ticket is built out of this field.
                TicketId = reply.TicketId,
                AuthorUsername = reply.AuthorUsername,
                CreatedUtc = reply.CreatedUtc
            }
        };
    }
}
