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

        // The network, not the address. A residential connection is renumbered by
        // the provider on its own schedule and a phone changes address every time it
        // leaves the house, so an exact comparison called half the logins of an
        // ordinary reader suspicious - and a warning that arrives on ordinary days
        // is one nobody reads on the day it matters. A prefix moves with the
        // provider and stays put across a reconnect.
        var knownNetworks = previousLogins
            .Select(entry => NetworkOf(entry.IpAddress))
            .Where(network => network != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var network = NetworkOf(ipAddress);
        if (network != null && knownNetworks.Count > 0 && !knownNetworks.Contains(network))
        {
            return true;
        }

        // The device is the other half, and it was accepted as an argument and never
        // read: a session opened from the owner's own network on a machine that has
        // never been seen - the case of a browser somebody else is sitting at - was
        // indistinguishable from the owner opening their laptop.
        //
        // Compared as the description, not as the raw header: the header carries a
        // build number that moves with every browser update, and comparing it would
        // call every update a new device.
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
