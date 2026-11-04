using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Community;

/// <summary>
/// Creates an in-app notification when a user is granted an award.
/// </summary>
internal class AwardGrantedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public AwardGrantedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.AwardGranted;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var awardData = await _dbContext.UserAwards
            .Where(a => a.UserAwardId == entityId && !a.IsRemoved)
            .Select(a => new
            {
                a.UserAwardId,
                a.UserId,
                a.User.Username,
                AwardTitle = a.AwardType.Title,
                AwardDescription = a.AwardType.Description,
                AwardTier = a.AwardType.Tier,
                a.AwardType.IconName,
                a.AwardedUtc,
                GrantedByUsername = a.AwardedBy.Username,
                ContestNumber = a.ContestSeries != null ? a.ContestSeries.Number : (int?)null,
                ContestYear = a.ContestSeries != null ? a.ContestSeries.Year : (int?)null,
            })
            .FirstOrDefaultAsync();

        if (awardData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [awardData.UserId],
            // No ActorId: a staff decision about the recipient, delivered whoever is on his blacklist.
            Metadata = new
            {
                awardData.UserAwardId,
                // The link to the hall of fame of the profile is built out of this field.
                awardData.Username,
                awardData.AwardTitle,
                awardData.AwardDescription,
                awardData.AwardTier,
                awardData.IconName,
                awardData.AwardedUtc,
                awardData.GrantedByUsername,
                awardData.ContestNumber,
                awardData.ContestYear,
            },
        };
    }
}
