using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace DM.Infrastructure.Core.Parsing.Tags;

internal static class BbAddress
{
    /// <summary>
    /// Host names that name the reader's own machine by definition (RFC 6761).
    /// </summary>
    private static readonly string[] BlockedHostNames = ["localhost"];

    private static readonly string[] AllowedSchemes = ["http://", "https://"];

    /// <summary>
    /// The text between the two tags when it is one address that may be put on
    /// a page, or null when it is anything else.
    /// </summary>
    /// <remarks>
    /// Whitespace around the address is forgiven, an editor leaves newlines around a
    /// pasted URL; inside it is refused, a URI carries none (RFC 3986), and it is
    /// what would turn one quoted attribute into two.
    /// </remarks>
    internal static string? Read(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.Length == 0 || trimmed.Any(IsSpaceOrControl) || !Accept(trimmed)
            ? null
            : trimmed;
    }

    /// <summary>
    /// The picture of a [tip]: an address <see cref="Read"/> accepts, or a path
    /// on this site - one slash, then anything but a second slash or a backslash
    /// - with the whitespace around it forgiven. Null for anything else.
    /// </summary>
    /// <remarks>
    /// The path lets a picture of the site be written once for the stand and the
    /// site. A backslash anywhere is refused, a browser reads it as a slash and
    /// "/\host" as another host.
    /// </remarks>
    internal static string? ReadTipImage(string raw)
    {
        var trimmed = raw.Trim();
        return IsSitePath(trimmed) ? trimmed : Read(raw);
    }

    private static bool IsSitePath(string value) =>
        value.Length > 1 && value[0] == '/' && value[1] != '/' &&
        !value.Any(c => c == '\\' || IsSpaceOrControl(c));

    private static bool IsSpaceOrControl(char c) => char.IsWhiteSpace(c) || char.IsControl(c);

    /// <summary>
    /// Whether the address may be put on a page: http or https only, and never
    /// the reader's own machine or network.
    /// </summary>
    private static bool Accept(string address) =>
        AllowedSchemes.Any(scheme => address.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) &&
        Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
        !IsBlockedHost(uri.Host);

    /// <summary>
    /// Whether the host names the reader's own machine or private network.
    /// </summary>
    /// <remarks>
    /// Not SSRF protection: the reader's browser requests the address, and this keeps
    /// a post from reaching services on the reader's loopback or LAN. Compared
    /// parsed, because no list of prefixes covers [::ffff:127.0.0.1] or fc00::/7. A
    /// name that resolves to a private address still passes: only a lookup would
    /// catch it, and this runs on the render path.
    /// </remarks>
    private static bool IsBlockedHost(string host)
    {
        // "localhost." with the dot of the DNS root is the same host to a browser.
        host = host.TrimEnd('.');

        // Uri.Host keeps the brackets around an IPv6 literal; IPAddress rejects them.
        var literal = host.Length > 1 && host[0] == '[' && host[^1] == ']'
            ? host[1..^1]
            : host;

        if (!IPAddress.TryParse(literal, out var address))
            return BlockedHostNames.Any(name =>
                host == name || host.EndsWith($".{name}", StringComparison.Ordinal));

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6UniqueLocal
                || address.Equals(IPAddress.IPv6Any);

        var octets = address.GetAddressBytes();
        return octets[0] switch
        {
            0 => true,                                  // "this network" — resolves to loopback on some stacks
            10 => true,                                 // RFC 1918
            100 => octets[1] >= 64 && octets[1] <= 127, // RFC 6598 shared address space
            169 => octets[1] == 254,                    // link-local, where cloud metadata lives
            172 => octets[1] >= 16 && octets[1] <= 31,  // RFC 1918
            192 => octets[1] == 168,                    // RFC 1918
            _ => false,
        };
    }
}
