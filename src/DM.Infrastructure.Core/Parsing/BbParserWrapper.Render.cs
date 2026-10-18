using DM.Infrastructure.Core.Parsing.Visitors;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Permission-aware rendering entry points. Parse once, filter by
/// <see cref="RenderContext"/>, emit in the requested audience format.
/// The filter and transform delegates come from
/// <see cref="PermissionFilteringVisitor"/>; they are pure functions of
/// the node and the context.
/// </summary>
public partial class BbParserWrapper
{
    /// <summary>
    /// Render the given BBCode to HTML, applying permission filtering
    /// per <paramref name="ctx"/>. Stripped privacy-sensitive subtrees
    /// produce zero output (no placeholder, no marker). For
    /// <see cref="RenderAudience.AuthorEdit"/> the caller must pass a
    /// wrapper constructed over the author-edit parser variant, whose
    /// tag templates emit <c>data-bb-*</c> round-trip attributes.
    /// </summary>
    public string RenderHtml(string input, RenderContext ctx)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var plan = PermissionFilteringVisitor.Prepare(ctx);
        var html = Parse(input).ToHtml(plan.Filter, plan.Transform);
        return ctx.Audience == RenderAudience.AuthorEdit ? LineBreaksWritten + html : html;
    }

    /// <summary>
    /// What the markup given to the author's editor starts with: every line
    /// break in it is written as a &lt;br /&gt;, and nothing else stands for one.
    /// </summary>
    /// <remarks>
    /// The editor reads two kinds of markup back into BBCode, its own visual
    /// mode and this one, and they say a line break differently. The visual mode
    /// has no break of its own between two blocks: a block stands on its own line
    /// there, and one written against its neighbour says so. Here a break is
    /// always written, so two blocks side by side were written side by side.
    /// Read by the visual mode's convention, [quote]a[/quote][quote]b[/quote]
    /// came back with a line break between the two, one more on every edit, and
    /// the page showed it. The marker tells htmlToBbcode which kind it reads.
    /// </remarks>
    public const string LineBreaksWritten = "<span data-bb-breaks=\"written\"></span>";

    /// <summary>
    /// Render the given BBCode to plain text, applying permission filtering.
    /// <see cref="RenderAudience.PlainText"/> is the canonical use case; other
    /// audiences are accepted but filter the same way.
    /// </summary>
    public string RenderText(string input, RenderContext ctx)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var plan = PermissionFilteringVisitor.Prepare(ctx);
        return Parse(input).ToText(plan.Filter, plan.Transform);
    }
}
