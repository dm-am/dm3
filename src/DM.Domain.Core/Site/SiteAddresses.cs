using System.Collections.Generic;

namespace DM.Domain.Core.Site;

/// <summary>
/// Every address this site answers on.
/// </summary>
/// <remarks>
/// <para>
/// A fact about the product, not a setting of a deployment: the list is the same
/// on a developer machine, on the stand and on the server, and it changes once in
/// a decade. Kept as configuration it was a list every deployment had to write
/// down and no deployment path ever did — the letter that names the other address
/// carried an empty line, on the one channel that still reaches a reader after the
/// address they use stops answering.
/// </para>
/// <para>
/// Hosts rather than URLs, canonical first, exactly as a visitor would type them.
/// The client keeps its own copy for the same reason it does not fetch this: the
/// moment somebody needs the other address is the moment the site stopped
/// answering, and a request cannot be served then. Two copies of two strings that
/// change once in a decade is the cheaper half of that trade, and an architecture
/// rule asserts the two agree.
/// </para>
/// </remarks>
public static class SiteAddresses
{
    /// <summary>The hosts, the canonical one first.</summary>
    public static IReadOnlyList<string> Hosts { get; } = ["dm.am", "ru.l.dm.am"];
}
