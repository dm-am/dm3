namespace DM.Domain.Community.Features.Catalogs;

/// <summary>
/// How long the text fields of an award type, an achievement category and an
/// achievement tier may be.
/// </summary>
/// <remarks>
/// The widths of the catalog columns, read by the entities, the validators and the
/// moderation windows alike: a validator below the column would be a second number
/// with no reason of its own, and one above it a save the database refuses.
/// </remarks>
public static class CatalogFieldLimits
{
    /// <summary>The stable code ("POSTS_100", "contest_first").</summary>
    public const int CodeMaxLength = 80;

    public const int TitleMaxLength = 200;

    public const int DescriptionMaxLength = 1000;

    /// <summary>An icon name of the game-icons set; the catalog of names is checked apart.</summary>
    public const int IconNameMaxLength = 80;
}
