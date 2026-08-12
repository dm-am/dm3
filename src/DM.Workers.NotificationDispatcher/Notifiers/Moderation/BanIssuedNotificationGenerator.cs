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

    /// <inheritdoc />
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
            // A ban keeps its row when it is lifted, so there is nothing to filter out:
            // the notification is about the moment it was issued.
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
            UsersInterested = new[] { banData.TargetUserId },
            // No ActorId although the moderator is right there in banData.AuthorId:
            // this is the notification that tells a person they are banned, and
            // blacklisting the moderator must not be a way to stop it arriving.
            Metadata = new
            {
                BanId = banData.BanId,
                Reason = banData.Comment,
                ModeratorUsername = banData.ModeratorUsername,
                EndedUtc = banData.EndedUtc,
                StartedUtc = banData.StartedUtc
            }
        };
    }
}
