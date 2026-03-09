using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Security;

/// <summary>
/// Generates security notification when user account is locked due to failed login attempts.
/// Notifies the user about the account lock for security awareness.
/// </summary>
internal class AccountLockedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public AccountLockedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.AccountLocked;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var userData = await _dbContext.Users
            .Where(u => u.UserId == entityId)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.Email
            })
            .FirstOrDefaultAsync();

        if (userData == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { userData.UserId },
            Metadata = new
            {
                Username = userData.Username,
                EventTime = DateTime.UtcNow
            }
        };
    }
}
