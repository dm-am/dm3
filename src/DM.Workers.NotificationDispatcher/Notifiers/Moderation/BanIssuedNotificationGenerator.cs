using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Moderation;

/// <summary>
/// Generates notification when a ban is issued to a user.
/// Notifies the banned user about the ban.
/// </summary>
internal class BanIssuedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public BanIssuedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.BanIssued;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var banData = await _dbContext.Bans
            // A ban keeps its row when it is lifted, so there is nothing to filter out.
            .Where(b => b.BanId == entityId)
            .Select(b => new
            {
                b.BanId,
                b.TargetUserId,
                TargetUsername = b.TargetUser.Username,
                b.Comment,
                b.AuthorId,
                ModeratorUsername = b.Author.Username,
                b.StartedUtc,
                b.EndedUtc
            })
            .FirstOrDefaultAsync();

        if (banData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [banData.TargetUserId],
            // No ActorId: blacklisting the moderator must not stop the ban notification.
            Metadata = new
            {
                banData.BanId,
                // The link to the profile is built out of this field.
                Username = banData.TargetUsername,
                Reason = banData.Comment,
                banData.ModeratorUsername,
                banData.EndedUtc,
                banData.StartedUtc
            }
        };
    }
}
