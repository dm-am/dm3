using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Security;

/// <summary>
/// The shared half of the account-security notifications. Each answers its own
/// event about one account and tells the owner of it; they read the account the
/// same way and differ only in the event and in the reason there is no actor.
/// The notification each hands back is written where the notification rules can
/// read it — in the generator's own file.
/// </summary>
internal abstract class SecurityNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    protected SecurityNotificationGenerator(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// The account the event is about, or null when it is gone: the events carry
    /// no idempotency key and are redelivered after a deletion.
    /// </summary>
    /// <param name="userId">User identifier the event carries</param>
    /// <returns>The account and the moment to report, or null</returns>
    protected async Task<SecurityEvent?> Subject(Guid userId)
    {
        var userData = await _dbContext.Users
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.UserId,
                u.Username
            })
            .FirstOrDefaultAsync();

        // As a DateTime rather than the offset the clock answers with: the letter
        // prints this field the way the serializer writes it down.
        return userData == null
            ? null
            : new SecurityEvent(userData.UserId, userData.Username, _dateTimeProvider.Now.UtcDateTime);
    }

    /// <summary>One account, and the moment the letter reports.</summary>
    protected sealed record SecurityEvent(Guid UserId, string Username, DateTime EventTime);
}
