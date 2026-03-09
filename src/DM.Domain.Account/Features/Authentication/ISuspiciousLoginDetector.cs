using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Detects suspicious login activity
/// </summary>
public interface ISuspiciousLoginDetector
{
    /// <summary>
    /// Check if the current login is suspicious (new device/IP)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="ipAddress">Current IP address</param>
    /// <param name="userAgent">Current user agent</param>
    /// <returns>True if login is suspicious</returns>
    Task<bool> IsSuspiciousAsync(Guid userId, string? ipAddress, string? userAgent);
}
