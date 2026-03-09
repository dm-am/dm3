using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Moderation;

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
            .Where(b => b.BanId == entityId && !b.IsRemoved)
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
