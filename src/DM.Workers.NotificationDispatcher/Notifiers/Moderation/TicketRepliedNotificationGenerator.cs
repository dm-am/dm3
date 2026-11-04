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
/// Only the writers TicketVisibility still lets in, minus the author; the rest learn from the queue.
/// </remarks>
internal class TicketRepliedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

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

        // The writers who are still on the staff, without System.
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
            // No ActorId: a reply reaching the moderators cannot depend on their blacklists.
            Metadata = new
            {
                // The link to the ticket is built out of this field.
                reply.TicketId,
                reply.AuthorUsername,
                reply.CreatedUtc
            }
        };
    }
}
