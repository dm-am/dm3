using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Moderation;

/// <summary>
/// Generates notification when a warning is issued to a user.
/// Notifies the warned user about the warning.
/// </summary>
internal class WarningIssuedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public WarningIssuedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.WarningIssued;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var warningData = await _dbContext.Warnings
            .Where(w => w.WarningId == entityId)
            .Select(w => new
            {
                w.WarningId,
                w.TargetUserId,
                TargetUsername = w.TargetUser.Username,
                w.Text,
                w.AuthorId,
                ModeratorUsername = w.Author.Username,
                w.CreatedUtc,
                w.Points
            })
            .FirstOrDefaultAsync();

        if (warningData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [warningData.TargetUserId],
            // No ActorId: a moderation decision is delivered whoever is on the blacklist.
            Metadata = new
            {
                warningData.WarningId,
                // The link to the log is narrowed by this field.
                Username = warningData.TargetUsername,
                Reason = warningData.Text,
                warningData.ModeratorUsername,
                warningData.Points,
                warningData.CreatedUtc
            }
        };
    }
}
