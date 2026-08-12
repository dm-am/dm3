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
/// Generates security notification when user email is changed.
/// Notifies the user about the email change for security awareness.
/// </summary>
internal class EmailChangedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public EmailChangedNotificationGenerator(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.EmailChanged;

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

        // As a DateTime rather than the offset the clock answers with: the letter
        // prints this field the way the serializer writes it down.
        var eventTime = _dateTimeProvider.Now.UtcDateTime;

        yield return new CreateNotification
        {
            UsersInterested = new[] { userData.UserId },
            // No ActorId: a system event. The owner changed their own email, so
            // there is no other person the recipient could have blacklisted.
            Metadata = new
            {
                Username = userData.Username,
                NewEmail = userData.Email,
                EventTime = eventTime
            }
        };
    }
}
