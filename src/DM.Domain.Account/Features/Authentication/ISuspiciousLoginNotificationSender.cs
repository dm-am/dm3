using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Sends mail about suspicious login activity
/// </summary>
public interface ISuspiciousLoginNotificationSender
{
    /// <summary>
    /// Send suspicious login notification to user's email
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="ipAddress">IP address of the login</param>
    /// <param name="userAgent">User agent string</param>
    /// <param name="timeZoneId">Zone the account keeps time in, null for UTC</param>
    Task SendAsync(string email, string? ipAddress, string? userAgent, string? timeZoneId);
}
