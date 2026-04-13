namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Semantic intent for BBCode rendering — drives visibility filtering
/// and output format. Audience is the intent, not the format.
/// </summary>
public enum RenderAudience
{
    /// <summary>
    /// Standard read surface. Privacy-sensitive tags ([private], [mod])
    /// are filtered per the viewer's permissions.
    /// </summary>
    Display = 0,

    /// <summary>
    /// Author loading own content into the editor. Both privacy tags
    /// are rendered verbatim with data-bb-* round-trip attributes so
    /// Tiptap can serialize them back to BBCode. Endpoint-level
    /// authorization must have confirmed authorship beforehand.
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
