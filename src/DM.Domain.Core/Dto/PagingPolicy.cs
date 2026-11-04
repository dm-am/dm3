namespace DM.Domain.Core.Dto;

/// <summary>
/// How large a page may be, for every list on the site at once.
/// </summary>
/// <remarks>
/// The client keeps its own copy of the numbers because it has to draw them;
/// PagingContractShould holds the two sides together.
/// </remarks>
public static class PagingPolicy
{
    /// <summary>
    /// Page sizes a reader may choose, as the account form offers them.
    /// </summary>
    public static readonly int[] AllowedPageSizes = [5, 10, 20, 30, 40, 50, 100, 200];

    /// <summary>
    /// The largest page any endpoint serves.
    /// </summary>
    /// <remarks>
    /// The ceiling and the last allowed size are the same number on purpose: a
    /// preference the reader can save and the API then refuses to serve is a
    /// setting that quietly does nothing.
    /// </remarks>
    public const int MaxPageSize = 200;

    /// <summary>
    /// Page size for a reader who has never chosen one.
    /// </summary>
    /// <remarks>
    /// The client sends this same number for a reader with no preference, so a
    /// different default here would only show up as a page that changes size the
    /// first time the settings are saved.
    /// </remarks>
    public const int DefaultPageSize = 20;

    /// <summary>Whether a page size is one of the allowed ones.</summary>
    /// <param name="value">Page size to check</param>
    public static bool Allows(int value) => AllowedPageSizes.Contains(value);
}
