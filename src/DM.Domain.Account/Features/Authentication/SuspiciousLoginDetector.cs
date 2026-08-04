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

        // The login being judged is already in this trail: it is written before
        // the caller gets here, so left in it makes its own address known to
        // itself and the answer is false for every login there has ever been. It
        // is the newest entry, because the trail comes back newest first.
        //
        // Only earlier successes count as evidence that an address is the
        // owner's. A failure records whoever was guessing, so treating failures
        // as history lets one wrong password launder the next one into "known".
        var previousLogins = loginHistory
            .Where(e => e.EventType == SecurityEventType.LoginSuccess)
            .Skip(1)
            .ToList();

        if (previousLogins.Count == 0)
        {
            // First login - not suspicious
            return false;
        }

        // Check if this IP has been seen before
        var knownIps = previousLogins
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
