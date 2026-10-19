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
/// A guest has no inbox. Spam is closed without the event.
/// </remarks>
internal class TicketResolvedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

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
            UsersInterested = [ticketData.UserId.Value],
            // No ActorId: a moderation decision is delivered whoever is on the blacklist.
            Metadata = new
            {
                // The link to the ticket is built out of this field.
                ticketData.TicketId,
                ticketData.ModeratorUsername,
                ticketData.CreatedUtc
            }
        };
    }
}
