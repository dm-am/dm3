using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Parsing;

namespace DM.Domain.Account.Features.Authentication;

/// <inheritdoc />
internal class SuspiciousLoginDetector : ISuspiciousLoginDetector
{
    private readonly ISecurityAuditRepository _auditRepository;

    public SuspiciousLoginDetector(ISecurityAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    /// <inheritdoc />
    public async Task<bool> IsSuspiciousAsync(Guid userId, string? ipAddress, string? userAgent)
    {
        if (string.IsNullOrEmpty(ipAddress) && string.IsNullOrEmpty(userAgent))
            return false;

        // Successes only: a failure records whoever was guessing, and failures must not
        // fill the window. The newest entry is the login being judged.
        var previousLogins = (await _auditRepository.GetByTypesAsync(userId, SecurityEventCategories.SuccessfulLogins, 20))
            .Skip(1)
            .ToList();

        if (previousLogins.Count == 0)
        {
            // First login - not suspicious
            return false;
        }

        // The network, not the address: a prefix stays put across a reconnect.
        var knownNetworks = previousLogins
            .Select(entry => NetworkOf(entry.IpAddress))
            .Where(network => network != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var network = NetworkOf(ipAddress);
        if (network != null && knownNetworks.Count > 0 && !knownNetworks.Contains(network))
        {
            return true;
        }

        // The device, compared as the description: the raw header changes with
        // every browser update.
        var knownDevices = previousLogins
            .Where(entry => !string.IsNullOrEmpty(entry.DeviceInfo))
            .Select(entry => entry.DeviceInfo!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(userAgent) && knownDevices.Count > 0)
        {
            return !knownDevices.Contains(UserAgentParser.Parse(userAgent));
        }

        return false;
    }

    /// <summary>
    /// The network an address belongs to: a /24 of IPv4, a /64 of IPv6.
    /// </summary>
    /// <remarks>
    /// Null for anything that does not parse, and null is not a match: an address
    /// nobody can read is not evidence that this login is the owner's, and it is not
    /// evidence that it is somebody else's either.
    /// </remarks>
    private static string? NetworkOf(string? address)
    {
        if (!IPAddress.TryParse(address, out var parsed))
        {
            return null;
        }

        var bytes = parsed.GetAddressBytes();
        var significant = parsed.AddressFamily == AddressFamily.InterNetworkV6 ? 8 : 3;

        return string.Join('.', bytes.Take(significant));
    }
}
