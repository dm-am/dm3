namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// How long the text fields of a tag and of a tag group may be.
/// </summary>
/// <remarks>
/// One declaration because creation and editing must agree. They already
/// disagreed once, for games: the create validator read a shared constant while
/// the edit validator kept its own numbers, so a title creation accepted could
/// not be saved again after any change to the row.
/// </remarks>
internal static class TagFieldLimits
{
    /// <summary>Longest title of a tag or of a group of them.</summary>
    public const int TitleMaxLength = 100;

    /// <summary>Longest description of a tag or of a group of them.</summary>
    public const int DescriptionMaxLength = 500;
}
