namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Configuration of the reverse proxies whose X-Forwarded-* headers are trusted
/// </summary>
public class ReverseProxyConfiguration
{
    /// <summary>
    /// CIDR networks the proxy may connect from (e.g. "172.16.0.0/12").
    /// Empty means the application is reachable directly and no forwarded
    /// header is honoured.
    /// </summary>
    public string[] TrustedNetworks { get; set; } = [];

    /// <summary>
    /// Number of proxies between the client and the application. Exactly this
    /// many entries are taken off the right end of X-Forwarded-For.
    /// </summary>
    public int ProxyCount { get; set; } = 1;
}
