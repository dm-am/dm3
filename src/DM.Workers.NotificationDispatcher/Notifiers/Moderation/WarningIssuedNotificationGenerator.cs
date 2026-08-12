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

    /// <inheritdoc />
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
            .Where(w => w.WarningId == entityId && !w.IsRemoved)
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
            UsersInterested = new[] { warningData.TargetUserId },
            // No ActorId although the moderator is right there in warningData.AuthorId:
            // a warning is not less delivered because the warned user has its author
            // on their blacklist, so this one stays outside the filter.
            Metadata = new
            {
                WarningId = warningData.WarningId,
                Reason = warningData.Text,
                ModeratorUsername = warningData.ModeratorUsername,
                Points = warningData.Points,
                CreatedUtc = warningData.CreatedUtc
            }
        };
    }
}
