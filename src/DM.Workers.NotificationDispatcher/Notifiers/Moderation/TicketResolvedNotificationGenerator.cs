using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Moderation;

/// <summary>
/// Generates notification when moderation closes a ticket.
/// Notifies the author of the ticket.
/// </summary>
/// <remarks>
/// The event was declared, sorted into the moderation category and sent by
/// nobody, so an author learned of the outcome only by opening "Мои обращения"
/// again. Only an author with an account is told: a guest has no inbox, and his
/// tracking page says the answer will appear there and nowhere else. An answer
/// in the conversation is an event of its own (TicketAnsweredNotificationGenerator);
/// this one says the ticket is over. Spam is closed without it: the service
/// sends no event for that.
/// </remarks>
internal class TicketResolvedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public TicketResolvedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.TicketResolved;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var ticketData = await _dbContext.Tickets
            .Where(t => t.TicketId == entityId)
            .Select(t => new
            {
                t.TicketId,
                t.UserId,
                ModeratorUsername = t.ResolvedBy != null ? t.ResolvedBy.Username : null,
                t.CreatedUtc
            })
            .FirstOrDefaultAsync();

        if (ticketData?.UserId == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { ticketData.UserId.Value },
            // No ActorId although the moderator who closed it is on the row: the
            // closing of a ticket is not less delivered because its author has the
            // moderator on his blacklist, as with every moderation decision.
            Metadata = new
            {
                // The page of the ticket is built out of this field, in the list
                // and in the letter alike.
                TicketId = ticketData.TicketId,
                ModeratorUsername = ticketData.ModeratorUsername,
                CreatedUtc = ticketData.CreatedUtc
            }
        };
    }
}
