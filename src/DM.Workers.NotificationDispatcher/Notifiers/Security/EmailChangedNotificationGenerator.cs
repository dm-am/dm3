using System;
using System.Collections.Generic;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Notifiers.Security;

/// <summary>
/// Generates security notification when user email is changed.
/// Notifies the user about the email change for security awareness.
/// </summary>
internal class EmailChangedNotificationGenerator : SecurityNotificationGenerator
{
    public EmailChangedNotificationGenerator(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.EmailChanged;

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
            // No ActorId: the owner changed his own email.
            Metadata = new
            {
                subject.Username,
                NewEmail = subject.Email,
                subject.EventTime
            }
        };
    }
}
