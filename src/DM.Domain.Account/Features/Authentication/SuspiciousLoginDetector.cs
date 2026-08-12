using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;

namespace DM.Domain.Account.Features.Authentication;

/// <inheritdoc />
internal class SuspiciousLoginDetector : ISuspiciousLoginDetector
{
    private readonly ISecurityAuditRepository _auditService;

    public SuspiciousLoginDetector(ISecurityAuditRepository auditService)
    {
        _auditService = auditService;
    }

    /// <inheritdoc />
    public async Task<bool> IsSuspiciousAsync(Guid userId, string? ipAddress, string? userAgent)
    {
        if (string.IsNullOrEmpty(ipAddress) && string.IsNullOrEmpty(userAgent))
            return false;

        // Successes only, and asked for as such. Only earlier successes count as
        // evidence that an address is the owner's: a failure records whoever was
        // guessing, so treating failures as history lets one wrong password
        // launder the next one into "known".
        //
        // Asking for the mixed trail and filtering it here made the window mean
        // twenty entries of any kind, so nineteen wrong passwords in a row
        // pushed every previous success out of it — and the login that followed
        // the guessing, the one this exists to catch, met an empty history and
        // was called ordinary.
        //
        // The login being judged is already in the list: it is written before
        // the caller gets here, so left in it makes its own address known to
        // itself and the answer is false for every login there has ever been. It
        // is the newest entry, because the trail comes back newest first.
        var previousLogins = (await _auditService.GetByTypesAsync(userId, SecurityEventCategories.SuccessfulLogins, 20))
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
