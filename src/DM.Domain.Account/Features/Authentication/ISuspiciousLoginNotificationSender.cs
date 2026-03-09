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
    /// <param name="username">Username</param>
    /// <param name="ipAddress">IP address of the login</param>
    /// <param name="userAgent">User agent string</param>
    Task SendAsync(string email, string username, string? ipAddress, string? userAgent);
}
