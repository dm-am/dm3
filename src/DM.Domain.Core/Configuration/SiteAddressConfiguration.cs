using System.Collections.Generic;

namespace DM.Domain.Core.Configuration;

/// <summary>
/// The addresses this site answers on
/// </summary>
/// <remarks>
/// One class because there is one subject. The addresses used to be split
/// between a settings class holding the address links are built from and a
/// mirror class holding the list of them, which meant the same address was
/// written down twice and the two could disagree.
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
    /// Origins a browser is allowed to call this API from
    /// </summary>
    /// <remarks>
    /// Read twice, by the CORS policy and by the origin check that stands in for
    /// a CSRF token. Both have to agree, which is the reason there is one list.
    /// </remarks>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Every address the site answers on, by identifier
    /// </summary>
    /// <remarks>
    /// Named so that a letter can carry them all. The one place a visitor is
    /// reachable after the address they use stops answering is their mailbox,
    /// and a letter that names only the address it was built from is no use
    /// there.
    ///
    /// A bare address, not a record with an identifier and a display name. The
    /// identifier is the key, so a field for it can only ever disagree with it,
    /// and what an address is called to a reader is a matter for the interface,
    /// which has its own list and its own words.
    /// </remarks>
    public Dictionary<string, string> Addresses { get; set; } = new();
}
