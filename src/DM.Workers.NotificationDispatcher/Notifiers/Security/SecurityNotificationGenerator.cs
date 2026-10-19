using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Security;

/// <summary>
/// The shared half of the account-security notifications.
/// </summary>
/// <remarks>
/// Each generator writes its notification in its own file, where the notification rules read it.
/// </remarks>
internal abstract class SecurityNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    protected SecurityNotificationGenerator(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// The account the event is about, or null once it is gone.
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
                u.Username,
                u.Email
            })
            .FirstOrDefaultAsync();

        // A DateTime: the letter prints the field as the serializer writes it.
        return userData == null
            ? null
            : new SecurityEvent(userData.UserId, userData.Username, userData.Email, _dateTimeProvider.Now.UtcDateTime);
    }

    /// <summary>One account, and the moment the letter reports.</summary>
    protected sealed record SecurityEvent(Guid UserId, string Username, string Email, DateTime EventTime);
}
