using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Site;

namespace DM.Domain.Core.Configuration;

/// <summary>
/// The addresses this site answers on
/// </summary>
/// <remarks>
/// What this deployment is, and only that. PublicUrl differs from stand to stand
/// and AdditionalOrigins exists for the development server of the client, so both
/// belong here. The list of addresses the site answers on does not differ between
/// deployments and therefore is not here — it is a fact of the product and lives
/// in <see cref="SiteAddresses" />. Kept here it was a list every deployment had
/// to fill in and none ever did.
///
/// There is no separate address for the API. The site and its API answer on one
/// origin, and the client asks for a path relative to wherever it loaded, so an
/// entry here is a site address and nothing else.
/// </remarks>
public class SiteAddressConfiguration
{
    /// <summary>
    /// Address this deployment answers on, and the root of every link it builds
    /// </summary>
    /// <remarks>
    /// Activation mails, password resets and notification bodies are all built
    /// from here. A host that generates none of them may leave it empty; a host
    /// that generates any of them declares so and refuses to start without it.
    /// </remarks>
    public string PublicUrl { get; set; } = null!;

    /// <summary>
    /// Origins that are not addresses of this site
    /// </summary>
    /// <remarks>
    /// The development server of the client is the whole of this list, and a
    /// deployment leaves it empty: everything the site answers on is declared
    /// below and derived from there, so nothing has to be written twice.
    ///
    /// It was written twice, and that is what this replaces. No path of any
    /// deployment ever put the public address of the stand into the list of
    /// allowed origins - not the installer, not the environment file, not the
    /// guide - so the first request to the first stand was refused at the door by
    /// the origin check, with the list it was compared against sitting two lines
    /// away in the same file.
    /// </remarks>
    public string[] AdditionalOrigins { get; set; } = [];

    /// <summary>
    /// Origins a browser may call this API from, derived rather than declared
    /// </summary>
    /// <remarks>
    /// Read twice, by the CORS policy and by the origin check that stands in for a
    /// CSRF token. Both have to agree, which is why there is one list - and why it
    /// is computed from the addresses above instead of being kept beside them.
    /// </remarks>
    public IReadOnlyList<string> BrowserOrigins() => SiteAddresses.Hosts
        .Select(host => $"https://{host}")
        .Prepend(PublicUrl)
        .Concat(AdditionalOrigins)
        .Select(OriginOf)
        .Where(origin => origin != null)
        .Select(origin => origin!)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// The origin of an address: scheme, host and a port only where it is not the
    /// default one. Null for anything that is not an absolute http(s) address.
    /// </summary>
    public static string? OriginOf(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}{port}";
    }
}
