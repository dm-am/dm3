using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

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
internal class SuspiciousLoginNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public SuspiciousLoginNotificationGenerator(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.SuspiciousLoginActivity;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var userData = await _dbContext.Users
            .Where(u => u.UserId == entityId)
            .Select(u => new
            {
                u.UserId,
                u.Username
            })
            .FirstOrDefaultAsync();

        if (userData == null)
        {
            yield break;
        }

        // As a DateTime rather than the offset the clock answers with: the letter
        // prints this field the way the serializer writes it down.
        var eventTime = _dateTimeProvider.Now.UtcDateTime;

        yield return new CreateNotification
        {
            UsersInterested = new[] { userData.UserId },
            // No ActorId: whoever logged in is either the owner or somebody the
            // account cannot name, and neither is an actor to link to.
            Metadata = new
            {
                Username = userData.Username,
                EventTime = eventTime
            }
        };
    }
}
