namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// The intent of a rendering, which drives the visibility filter.
/// </summary>
public enum RenderAudience
{
    /// <summary>
    /// Standard read surface. [private] is filtered per the viewer's
    /// permissions; [mod] is public on read and is always rendered.
    /// </summary>
    Display = 0,

    /// <summary>
    /// The author loading own content into the editor: the stored BBCode as
    /// typed, nothing rendered. A missing author id at render time yields Display.
    /// </summary>
    AuthorEdit = 1,

    /// <summary>
    /// Plain-text channel (mail, search). Hiding tags are always stripped.
    /// </summary>
    PlainText = 2,

    /// <summary>
    /// Embed: the NSFW-safe tag set, hiding tags always stripped.
    /// </summary>
    EmbedSafe = 3
}
