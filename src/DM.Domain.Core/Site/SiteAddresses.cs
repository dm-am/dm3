using System.Collections.Generic;
using System.Linq;

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
/// Hosts rather than URLs, canonical first, exactly as a visitor would type them,
/// and each with the name the site calls it by: a host on its own says nothing
/// to the reader who meets it in a letter, and this list exists precisely for
/// the reader whose usual address stopped answering.
/// The client keeps its own copy for the same reason it does not fetch this: the
/// moment somebody needs the other address is the moment the site stopped
/// answering, and a request cannot be served then. Two copies of two strings that
/// change once in a decade is the cheaper half of that trade, and an architecture
/// rule asserts the two agree.
/// </para>
/// </remarks>
public static class SiteAddresses
{
    /// <summary>The addresses, the canonical one first.</summary>
    public static IReadOnlyList<SiteAddress> All { get; } =
    [
        new("Основной сервер", "dm.am"),
        new("RU-прокси", "ru.l.dm.am"),
    ];

    /// <summary>The hosts alone, in the same order.</summary>
    public static IReadOnlyList<string> Hosts { get; } = All.Select(address => address.Host).ToList();
}

/// <summary>
/// One address of the site.
/// </summary>
/// <param name="Name">What the site calls this address</param>
/// <param name="Host">The host itself, as a visitor would type it</param>
public record SiteAddress(string Name, string Host);
