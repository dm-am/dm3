using System;
using System.Collections.Generic;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Notifiers.Security;

/// <summary>
/// Generates security notification when user password is changed.
/// Notifies the user about the password change for security awareness.
/// </summary>
internal class PasswordChangedNotificationGenerator : SecurityNotificationGenerator
{
    public PasswordChangedNotificationGenerator(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.PasswordChanged;

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
            UsersInterested = [subject.UserId],
            // No ActorId: the owner changed his own password.
            Metadata = new
            {
                subject.Username,
                subject.EventTime
            }
        };
    }
}
