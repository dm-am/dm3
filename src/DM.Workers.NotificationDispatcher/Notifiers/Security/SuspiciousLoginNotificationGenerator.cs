using System;
using System.Collections.Generic;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Notifiers.Security;

/// <summary>
/// Generates the security notification for a login from an address or a device
/// the account has not been seen on before.
/// </summary>
/// <remarks>
/// No address or device in the payload: the letter sent at the login already names them.
/// </remarks>
internal class SuspiciousLoginNotificationGenerator : SecurityNotificationGenerator
{
    public SuspiciousLoginNotificationGenerator(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.SuspiciousLoginActivity;

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
            // No ActorId: whoever logged in is the owner or somebody the account cannot name.
            Metadata = new
            {
                subject.Username,
                subject.EventTime
            }
        };
    }
}
