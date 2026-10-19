namespace DM.Domain.Blog.Features.Publications;

/// <summary>
/// How long the text fields of a publication may be.
/// </summary>
internal static class PublicationFieldLimits
{
    public const int TitleMaxLength = 300;

    public const int PreviewMaxLength = 500;
}
