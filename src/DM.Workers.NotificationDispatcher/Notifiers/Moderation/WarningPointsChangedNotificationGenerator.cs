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
/// The warning itself is never withdrawn, so this is the only thing that ever
/// happens to one after it is issued, and it happened in silence: a moderator
/// took points off by mistake and the person carrying them found out by opening
/// the log. The payload repeats everything the issuing notification carries, so
/// that a reader who no longer has the first message still knows which warning
/// this is about, and adds the two numbers and the sentence written under them.
/// </remarks>
internal class WarningPointsChangedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
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
                // Left join: the column is null until somebody corrects the
                // warning, and a redelivered event about an uncorrected one must
                // answer with a notification rather than throw.
                ChangedByUsername = w.PointsChangedBy!.Username
            })
            .FirstOrDefaultAsync();

        if (warningData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { warningData.TargetUserId },
            // No ActorId, for the reason the issuing notification gives: a
            // moderation decision is not less delivered because the reader has
            // its author on their blacklist.
            Metadata = new
            {
                WarningId = warningData.WarningId,
                // The name of the warned reader: both the list and the letter
                // build the way into his own log out of this one field.
                Username = warningData.TargetUsername,
                Reason = warningData.Text,
                ModeratorUsername = warningData.ModeratorUsername,
                OriginalPoints = warningData.OriginalPoints,
                Points = warningData.Points,
                PointsChangeText = warningData.PointsChangeText,
                PointsChangedByUsername = warningData.ChangedByUsername,
                CreatedUtc = warningData.CreatedUtc
            }
        };
    }
}
