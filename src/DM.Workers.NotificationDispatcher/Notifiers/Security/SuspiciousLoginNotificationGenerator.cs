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
/// The event type, its wording and its category all existed and nothing produced
/// it: the settings screen offered the reader a Security category to subscribe
/// to, and one of the four things in that category was never sent. The letter
/// that goes out at the moment of the login is the other half of this and stays
/// where it is - it is unconditional, while everything the dispatcher sends by
/// mail is opt-in and off by default.
///
/// The address and the device are deliberately absent from the payload. The event
/// carries an identifier and nothing else, the journal entry that holds them lives
/// in the document store, and the letter that does name them has already gone.
/// </remarks>
internal class SuspiciousLoginNotificationGenerator : SecurityNotificationGenerator
{
    /// <inheritdoc />
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
            UsersInterested = new[] { subject.UserId },
            // No ActorId: whoever logged in is either the owner or somebody the
            // account cannot name, and neither is an actor to link to.
            Metadata = new
            {
                Username = subject.Username,
                EventTime = subject.EventTime
            }
        };
    }
}
