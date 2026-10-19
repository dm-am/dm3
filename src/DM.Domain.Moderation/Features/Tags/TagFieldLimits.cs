namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// How long the text fields of a tag and of a tag group may be, on creation and on editing alike.
/// </summary>
internal static class TagFieldLimits
{
    public const int TitleMaxLength = 100;

    public const int DescriptionMaxLength = 500;
}
