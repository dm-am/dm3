using DM.Domain.Core.Site;

namespace DM.Domain.Core.Configuration;

/// <summary>
/// The addresses this site answers on
/// </summary>
/// <remarks>
/// Only what differs between deployments: the addresses of the product live in
/// <see cref="SiteAddresses" />, and the site and its API share one origin.
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
    /// Only the development server of the client: the site's own origins are
    /// derived below.
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
        .OfType<string>()
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
