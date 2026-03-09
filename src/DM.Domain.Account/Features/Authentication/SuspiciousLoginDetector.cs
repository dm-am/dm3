using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;

namespace DM.Domain.Account.Features.Authentication;

/// <inheritdoc />
internal class SuspiciousLoginDetector : ISuspiciousLoginDetector
{
    private readonly ISecurityAuditService _auditService;

    public SuspiciousLoginDetector(ISecurityAuditService auditService)
    {
        _auditService = auditService;
    }

    /// <inheritdoc />
    public async Task<bool> IsSuspiciousAsync(Guid userId, string? ipAddress, string? userAgent)
    {
        if (string.IsNullOrEmpty(ipAddress) && string.IsNullOrEmpty(userAgent))
            return false;

        // Get recent login history
        var loginHistory = await _auditService.GetLoginHistoryAsync(userId, 20);

        if (!loginHistory.Any())
        {
            // First login - not suspicious
            return false;
        }

        // Check if this IP has been seen before
        var knownIps = loginHistory
            .Where(e => !string.IsNullOrEmpty(e.IpAddress))
            .Select(e => e.IpAddress)
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // If IP is new and we have history, it's potentially suspicious
        if (!string.IsNullOrEmpty(ipAddress) && knownIps.Count > 0 && !knownIps.Contains(ipAddress))
        {
            return true;
        }

        return false;
    }
}
