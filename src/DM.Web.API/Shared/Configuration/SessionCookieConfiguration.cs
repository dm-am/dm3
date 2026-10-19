namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Attributes of the session cookie that the deployment decides
/// </summary>
/// <remarks>
/// A cookie is HTTP, and this host is the only process that writes one.
/// </remarks>
public class SessionCookieConfiguration
{
    /// <summary>
    /// Domain the session cookie is scoped to. Empty means host-only.
    /// </summary>
    /// <remarks>
    /// A named domain hands the cookie to every host under it, so one session covers
    /// every address of the site. Empty by default: that is safe only once every host
    /// under the domain is this application.
    /// </remarks>
    public string Domain { get; set; } = "";
}
