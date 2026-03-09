using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Moderation;

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
                TargetUsername = t.Target.Username,
                t.UserId,
                AuthorUsername = t.Author.Username,
                t.CreatedUtc
            })
            .FirstOrDefaultAsync();

        if (ticketData == null)
        {
            yield break;
        }

        // Get all moderators and admins to notify
        var moderatorIds = await _dbContext.Users
            .Where(u => !u.IsRemoved)
            .Where(u => u.Role == UserRole.Moderator || u.Role == UserRole.Admin || u.Role == UserRole.SeniorModerator)
            .Select(u => u.UserId)
            .ToArrayAsync();

        if (moderatorIds.Length == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = moderatorIds,
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
