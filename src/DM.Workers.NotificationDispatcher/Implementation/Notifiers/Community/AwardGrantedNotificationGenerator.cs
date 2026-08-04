using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Community;

/// <summary>
/// Creates an in-app notification when a user is granted an award.
/// The recipient is notified: which award, in which contest series
/// and who granted it — everything needed to open the profile and see details.
/// </summary>
internal class AwardGrantedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public AwardGrantedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.AwardGranted;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.UserAwards
            .Where(a => a.UserAwardId == entityId && !a.IsRemoved)
            .Select(a => new
            {
                a.UserAwardId,
                a.UserId,
                AwardTitle = a.AwardType.Title,
                AwardDescription = a.AwardType.Description,
                AwardTier = a.AwardType.Tier,
                IconName = a.AwardType.IconName,
                a.AwardedUtc,
                GrantedByUsername = a.AwardedBy.Username,
                ContestNumber = (int?)(a.ContestSeries != null ? a.ContestSeries.Number : (int?)null),
                ContestYear = (int?)(a.ContestSeries != null ? a.ContestSeries.Year : (int?)null),
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { data.UserId },
            // ActorId stays null on purpose, and not because there is nobody to
            // put there: the award was granted by an admin or a senior mod, and
            // AwardedByUserId holds them. It is a staff decision about the
            // recipient's own profile, of the same kind as a warning, so it must
            // not become undeliverable when the recipient has that admin on their
            // blacklist.
            Metadata = new
            {
                UserAwardId = data.UserAwardId,
                AwardTitle = data.AwardTitle,
                AwardDescription = data.AwardDescription,
                AwardTier = data.AwardTier,
                IconName = data.IconName,
                AwardedUtc = data.AwardedUtc,
                GrantedByUsername = data.GrantedByUsername,
                ContestNumber = data.ContestNumber,
                ContestYear = data.ContestYear,
            },
        };
    }
}
