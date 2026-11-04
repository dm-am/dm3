using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Moderation;

/// <summary>
/// Generates notification when the points of a warning are changed.
/// Notifies the warned user about the correction.
/// </summary>
/// <remarks>
/// The payload repeats the issuing notification, so the reader knows which warning it is about.
/// </remarks>
internal class WarningPointsChangedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public WarningPointsChangedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.WarningPointsChanged;

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
                ModeratorUsername = w.Author.Username,
                w.CreatedUtc,
                w.Points,
                w.OriginalPoints,
                w.PointsChangeText,
                // Left join: the column is null until somebody corrects the warning.
                ChangedByUsername = w.PointsChangedBy!.Username
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
                // The link to the log is built out of this field.
                Username = warningData.TargetUsername,
                Reason = warningData.Text,
                warningData.ModeratorUsername,
                warningData.OriginalPoints,
                warningData.Points,
                warningData.PointsChangeText,
                PointsChangedByUsername = warningData.ChangedByUsername,
                warningData.CreatedUtc
            }
        };
    }
}
