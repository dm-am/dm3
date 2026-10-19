namespace DM.Domain.Core.Site;

/// <summary>
/// Every address this site answers on.
/// </summary>
/// <remarks>
/// A fact about the product, not a setting of a deployment. Hosts as a visitor
/// types them, canonical first, each with its two names. The client keeps its own copy,
/// because the other address is needed exactly when the site stops answering;
/// an architecture rule asserts the two agree.
/// </remarks>
public static class SiteAddresses
{
    /// <summary>The addresses, the canonical one first.</summary>
    public static IReadOnlyList<SiteAddress> All { get; } =
    [
        new("Основной", "Основной сервер", "dm.am"),
        new("RU-прокси", "RU-прокси", "ru.l.dm.am"),
    ];

    /// <summary>The hosts alone, in the same order.</summary>
    public static IReadOnlyList<string> Hosts { get; } = All.Select(address => address.Host).ToList();
}

/// <summary>
/// One address of the site.
/// </summary>
/// <param name="Name">
/// What the address is called under the heading of the sidebar block "Серверы
/// сайта", which gives the word its context: "Основной"
/// </param>
/// <param name="Title">
/// The full name, for a sentence with no heading over it, such as the link under
/// the button of a letter and the failure toast: "Ссылка через Основной сервер"
/// </param>
/// <param name="Host">The host itself, as a visitor would type it</param>
public record SiteAddress(string Name, string Title, string Host);
