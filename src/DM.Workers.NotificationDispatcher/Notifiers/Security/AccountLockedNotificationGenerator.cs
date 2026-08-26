using System;
using System.Collections.Generic;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Notifiers.Security;

/// <summary>
/// Generates security notification when user account is locked due to failed login attempts.
/// Notifies the user about the account lock for security awareness.
/// </summary>
internal class AccountLockedNotificationGenerator : SecurityNotificationGenerator
{
    /// <inheritdoc />
    public AccountLockedNotificationGenerator(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.AccountLocked;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var subject = await Subject(entityId);
        if (subject == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { subject.UserId },
            // No ActorId: a system event. The lock is raised by the failed-login
            // counter, not by a person, and it goes to the owner of the account.
            Metadata = new
            {
                Username = subject.Username,
                EventTime = subject.EventTime
            }
        };
    }
}
