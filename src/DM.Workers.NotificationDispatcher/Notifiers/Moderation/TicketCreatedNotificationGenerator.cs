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
/// Generates notification when a new moderation ticket is created.
/// Notifies the moderators the ticket is moderation's business of.
/// </summary>
/// <remarks>
/// The notification carries the text of the ticket, so its audience is TicketVisibility's, minus the author.
/// </remarks>
internal class TicketCreatedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public TicketCreatedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.TicketCreated;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var ticketData = await _dbContext.Tickets
            .Where(t => t.TicketId == entityId)
            .Select(t => new
            {
                t.TicketId,
                t.Subtype,
                t.Description,
                t.TargetId,
                TargetUsername = t.Target != null ? t.Target.Username : null,
                t.UserId,
                AuthorUsername = t.Author != null ? t.Author.Username : null,
                t.CreatedUtc
            })
            .FirstOrDefaultAsync();

        if (ticketData == null)
        {
            yield break;
        }

        // Staff by the ordinal role comparison, without System; TicketVisibility narrows it below.
        var staff = await _dbContext.Users
            .Where(u => !u.IsRemoved)
            .Where(u => u.Role >= UserRole.Moderator && u.Role < UserRole.System)
            .Select(u => new { u.UserId, u.Role })
            .ToArrayAsync();

        var moderatorIds = staff
            .Where(u => u.UserId != ticketData.UserId)
            .Where(u => TicketVisibility.Moderates(u.UserId, u.Role, ticketData.Subtype, ticketData.TargetId))
            .Select(u => u.UserId)
            .ToArray();

        if (moderatorIds.Length == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = moderatorIds,
            // No ActorId: a report reaching the moderators cannot depend on their blacklists.
            Metadata = new
            {
                ticketData.TicketId,
                ticketData.Description,
                ticketData.TargetUsername,
                ticketData.AuthorUsername,
                ticketData.CreatedUtc
            }
        };
    }
}
