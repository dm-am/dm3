using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Parsing;
using DM.Infrastructure.Core.Parsing;

namespace DM.Infrastructure.Persistence.Repositories.Search;

/// <summary>
/// The visible text of a body, as a reader would see it with the markup gone.
/// </summary>
/// <remarks>
/// <para>
/// Written beside the body it comes from, because both things that read it need
/// plain text and neither can produce it. The index tokenised raw BBCode, so a
/// tag name was a word one could search for; the preview cut raw BBCode at a
/// fixed length, so what a reader saw in the results was a fragment of markup,
/// sometimes ending in the middle of a tag.
/// </para>
/// <para>
/// The surface matters and is not a detail. [private] is the one tag filtered by
/// audience, and it is only a node of the tree on the surfaces that declare it —
/// rendered on the wrong surface it survives as a literal, and its contents would
/// then reach both the index and the preview. So the surface here must be the one
/// the body is displayed on.
/// </para>
/// <para>
/// The regexp in the generated column stays as a second lock. Two spellings of
/// one rule is a cost; a projection that silently stops filtering is worse, and
/// this one runs in a process that can fail while the column cannot.
/// </para>
/// </remarks>
internal static class SearchTextProjection
{
    private static readonly IBbParserProvider Provider = new BbParserProvider();

    /// <summary>Line breaks the renderer emits as tags rather than as characters.</summary>
    private static readonly Regex Breaks = new(@"<(?:br|hr)\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// The visible text of a body written for the given surface.
    /// </summary>
    public static string Of(string? body, BbSurface surface)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        string rendered;
        try
        {
            var parser = (BbParserWrapper)Provider.GetForSurface(surface);
            rendered = parser.RenderText(body, RenderContext.ForPlainText() with { Surface = surface });
        }
        catch
        {
            // A body the parser refuses still has to be searchable, and the one
            // thing that must not leak either way is the private block. The regexp
            // is the same one the generated column applies.
            return Collapse(SearchSnippet.StripPrivateBlocks(body));
        }

        // The plain-text render is not plain text: it emits the line breaks as
        // tags and leaves entities encoded, so "a < b" comes back as "a &lt; b"
        // and would be indexed and previewed that way.
        rendered = Breaks.Replace(rendered, " ");
        rendered = WebUtility.HtmlDecode(rendered);

        return Collapse(rendered);
    }

    /// <summary>
    /// Control characters out, runs of whitespace down to one space.
    /// </summary>
    /// <remarks>
    /// The control characters are not decoration: the preview marks a match with
    /// two of them, and one arriving from a body would be read as a mark.
    /// </remarks>
    private static string Collapse(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return Whitespace.Replace(builder.ToString(), " ").Trim();
    }
}
