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
    Task<bool> IsSuspiciousAsync(Guid userId, string? ipAddress, string? userAgent);
}
