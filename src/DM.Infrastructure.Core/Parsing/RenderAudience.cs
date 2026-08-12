namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Semantic intent for BBCode rendering — drives visibility filtering
/// and output format. Audience is the intent, not the format.
/// </summary>
public enum RenderAudience
{
    /// <summary>
    /// Standard read surface. [private] is filtered per the viewer's
    /// permissions; [mod] is public on read and is always rendered.
    /// </summary>
    Display = 0,

    /// <summary>
    /// Author loading own content into the editor. Both privacy tags
    /// are rendered verbatim with data-bb-* round-trip attributes so
    /// Tiptap can serialize them back to BBCode. Authorship is verified
    /// per field at render time: the endpoint only supplies the author id
    /// in the render envelope, and a missing id means "not the author"
    /// and yields Display.
    /// </summary>
    AuthorEdit = 1,

    /// <summary>
    /// Plain-text channel (email, notification digests, search index).
    /// Privacy-sensitive tags are unconditionally stripped. Viewer
    /// is treated as anonymous.
    /// </summary>
    PlainText = 2,

    /// <summary>
    /// Link preview / cross-post embed. Uses the NSFW-safe tag set
    /// and unconditionally strips privacy-sensitive tags.
    /// </summary>
    EmbedSafe = 3
}
