using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Moderation;

/// <summary>
/// Generates notification when a new moderation ticket is created.
/// Notifies moderators about the new ticket.
/// </summary>
internal class TicketCreatedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
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

        // Staff, by the ordinal comparison every other role check in the codebase
        // uses. The enumerated form it replaces had to be edited by hand for every
        // new role, and System is excluded because the robot account cannot read.
        var moderatorIds = await _dbContext.Users
            .Where(u => !u.IsRemoved)
            .Where(u => u.Role >= UserRole.Moderator && u.Role < UserRole.System)
            .Select(u => u.UserId)
            .ToArrayAsync();

        if (moderatorIds.Length == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = moderatorIds,
            // No ActorId although the ticket author is right there in ticketData.UserId:
            // the recipients are the moderators on duty, and a report reaching them
            // cannot depend on whether one of them blacklisted the reporter.
            Metadata = new
            {
                TicketId = ticketData.TicketId,
                Description = ticketData.Description,
                TargetUsername = ticketData.TargetUsername,
                AuthorUsername = ticketData.AuthorUsername,
                CreatedUtc = ticketData.CreatedUtc
            }
        };
    }
}
