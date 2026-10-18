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
/// The notification carries the text of the ticket and the name of its author, so
/// its audience is the audience of the ticket itself, by the one rule the ticket
/// service and the queue read (TicketVisibility): a moderator whose role reads the
/// category and who is not the violator it names. Every moderator on duty used to
/// be told, and so a junior moderator read the text of an access recovery request
/// he cannot open, and a moderator read a complaint about himself together with
/// the name of whoever filed it. The author is left out as well: a moderator who
/// filed a complaint has nothing to learn from its filing.
/// </remarks>
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

        // Staff, by the ordinal comparison every other role check in the codebase
        // uses, and System is excluded because the robot account cannot read. This
        // only narrows the read: who of the staff is told is TicketVisibility's
        // answer, asked of each below, and the staff of a site is a handful of rows.
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
