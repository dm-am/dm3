using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>
/// The address an image or a link may point at.
/// </summary>
internal static class BbAddress
{
    /// <summary>
    /// Host names that name the reader's own machine by definition — RFC 6761
    /// reserves them for exactly that, so no lookup is needed to know it.
    /// </summary>
    private static readonly string[] BlockedHostNames = ["localhost"];

    /// <summary>Allowed URL schemes for links and images.</summary>
    private static readonly string[] AllowedSchemes = ["http://", "https://"];

    /// <summary>
    /// The text between the two tags when it is one address that may be put on
    /// a page, or null when it is anything else.
    /// </summary>
    /// <remarks>
    /// Whitespace around the address is forgiven — an editor leaves newlines
    /// around a pasted URL — and whitespace inside it is not, because a URI
    /// carries none (RFC 3986). An address that is not one, and one that is
    /// refused (see <see cref="Accept"/>), make the tag the text that was typed,
    /// brackets and address included. A refused image used to disappear and a
    /// refused link to leave its words without the link, and neither the author
    /// nor a reader could tell what had not worked.
    /// </remarks>
    internal static string? Read(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.Length == 0 || trimmed.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) ||
               !Accept(trimmed)
            ? null
            : trimmed;
    }

    /// <summary>
    /// The picture of a [tip]: an address <see cref="Read"/> accepts, or a path
    /// on this site - one slash, then anything but a second slash or a backslash
    /// - with the whitespace around it forgiven. Null for anything else.
    /// </summary>
    /// <remarks>
    /// The path is the one thing added to the rule of [img]. Without it a
    /// picture from the site's own folder could not be written at all: the
    /// stand and the site have different addresses. It gives nothing [img] does
    /// not have, [img] taking a full address of this same site. "//host" and
    /// "/\host" are refused, because a browser reads both as another host, and
    /// so is a backslash anywhere, which a browser reads as a slash; whitespace
    /// and control characters inside the path are refused for the reason they
    /// are in any address. The client's check is the same (bbcode.ts,
    /// isTipImageAddress).
    /// </remarks>
    internal static string? ReadTipImage(string raw)
    {
        var trimmed = raw.Trim();
        return IsSitePath(trimmed) ? trimmed : Read(raw);
    }

    private static bool IsSitePath(string value) =>
        value.Length > 1 && value[0] == '/' && value[1] != '/' &&
        !value.Any(c => c == '\\' || char.IsWhiteSpace(c) || char.IsControl(c));

    /// <summary>
    /// Whether the address may be put on a page: http or https only, and never
    /// the reader's own machine or network.
    /// </summary>
    /// <remarks>
    /// A URI carries no raw whitespace and no control characters (RFC 3986), and
    /// one that does is not merely malformed — it is the lever that turns one
    /// quoted attribute into two, so this method refuses it on its own.
    /// </remarks>
    private static bool Accept(string address)
    {
        if (string.IsNullOrEmpty(address) ||
            address.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            return false;

        var lower = address.ToLowerInvariant();
        if (!AllowedSchemes.Any(scheme => lower.StartsWith(scheme, StringComparison.Ordinal)))
            return false;

        try
        {
            return !IsBlockedHost(new Uri(address).Host.ToLowerInvariant());
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the host names the reader's own machine or private network.
    /// </summary>
    /// <remarks>
    /// Naming the risk right matters here, because it was recorded wrong: the
    /// server never requests these addresses — the reader's browser does, when it
    /// renders the src/href — so this is not SSRF protection. What it prevents is
    /// someone else's post reaching services the reader happens to run on loopback
    /// or on their LAN. A server-side fetch of a user-supplied URL is a separate
    /// threat and needs its own check, made after DNS resolution against the
    /// address the connection actually goes to.
    ///
    /// The address has to be compared parsed rather than as a string prefix.
    /// `new Uri` folds 127.1, 2130706433 and 0177.0.0.1 into the same four bytes,
    /// so those spellings used to be covered by accident, but it leaves an
    /// IPv4-mapped literal such as [::ffff:127.0.0.1] exactly as written, and no
    /// list of prefixes covers the IPv6 ranges — fd00: is one address block out of
    /// the whole of fc00::/7. A host NAME that resolves to a private address
    /// (wildcard-DNS services point any name at 127.0.0.1) still passes: only a
    /// lookup would catch it, and this runs on the render path.
    /// </remarks>
    private static bool IsBlockedHost(string host)
    {
        // A name written with the dot of the DNS root at its end is the same
        // name: the browser goes to "localhost." as it goes to "localhost", and
        // reads "127.0.0.1." as 127.0.0.1. Compared as written, both passed.
        // The editor has always trimmed it (bbcode.ts, isBlockedHost).
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
