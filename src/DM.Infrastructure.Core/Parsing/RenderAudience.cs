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
    EmbedSafe = 3,

    /// <summary>
    /// Source of a quotation: BBCode, not HTML, for the reply the reader is
    /// about to write. Privacy-sensitive tags are stripped unconditionally -
    /// for the author of the post and for a game lead as well - and a
    /// quotation already inside the text is dropped with its content.
    /// </summary>
    /// <remarks>
    /// Quoting is republication. The addressee snapshot of a [private] block
    /// does not travel with the quotation: in the new post the same block would
    /// be visible by the new post's rules, that is to a different set of people.
    /// So the answer is not "filter it for this reader" but "there is never a
    /// private block in a quotation".
    ///
    /// Deliberately unreachable from <see cref="BbAudienceHeader"/>. The header
    /// is client-controlled and applies to every BbText field of every response,
    /// while this audience emits BBCode source rather than HTML - text that no
    /// security substitution has run over, because the point of the source is to
    /// give back what the author typed. Parsed by the header it would put raw
    /// author text into fields the client binds through v-html. It is selected
    /// by the endpoint that is asked for a quotation and by nothing else.
    /// </remarks>
    QuoteSource = 4
}
