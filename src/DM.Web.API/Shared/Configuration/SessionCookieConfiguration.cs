namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Attributes of the session cookie that the deployment decides
/// </summary>
/// <remarks>
/// A cookie is HTTP, and this host is the only process that writes one: the
/// account domain issues sessions and has no opinion on how they travel. Kept
/// here rather than beside the session lifetimes, which the seeder and the
/// notification dispatcher bind as well - neither of them answers a request, and
/// a setting they can only ignore reads as one they might use.
/// </remarks>
public class SessionCookieConfiguration
{
    /// <summary>
    /// Domain the session cookie is scoped to. Empty means host-only.
    /// </summary>
    /// <remarks>
    /// A host-only cookie belongs to the exact name that issued it, so a visitor
    /// who follows a link to another address of the same site arrives
    /// unauthenticated and signs in again. Naming the registrable domain here
    /// hands the cookie to every host under it, and one session then covers all
    /// the addresses the site answers on.
    ///
    /// Empty by default, and deliberately: the value is only safe once every
    /// host under that domain is this application. A domain shared with anything
    /// else hands that thing the session cookie of every visitor.
    /// </remarks>
    public string Domain { get; set; } = "";
}
