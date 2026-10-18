using System;
using System.Text;
using System.Text.RegularExpressions;
using DM.Domain.Core.Content;
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
/// The surface matters and is not a detail: the tag set differs between surfaces
/// ([mod] is markup on some and text on others), so the surface here must be the
/// one the body is displayed on. [private] is a node on every surface, and the
/// plain text drops it on every one.
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
            // thing that must not leak either way is the private block. Without
            // the tree nothing can say where a block ends: the pattern of the
            // second lock ends one at the first [/private] and sees neither a
            // block inside a block nor a [/private] shown as an example inside
            // [noparse], so cutting blocks out with it left the rest of an outer
            // block in the index. What is kept is the text before anything that
            // could open a block, and nothing after it: a block starts at its
            // opening, so no hidden word stands before the first one. The text is
            // the one the parser would have read: a character the parser removes
            // joins what stood around it, and "[pri" U+0001 "vate]" is a hidden
            // block on the page.
            return Collapse(BeforeHidingMarkup(ParserInput.Of(body)));
        }

        // The plain text is text: no entities to read back and no markup for
        // the line breaks, which Collapse turns into spaces with the rest.
        return Collapse(rendered);
    }

    /// <summary>
    /// The text up to the first "[private" in any case, or all of it.
    /// </summary>
    /// <remarks>
    /// Compared ignoring case the way the runtime does it, which folds more than
    /// A-Z: a spelling it reads as the tag and the grammar does not ("[prıvate",
    /// with a dotless i) cuts the text sooner than needed. Here that errs the safe
    /// way, towards indexing less.
    /// </remarks>
    private static string BeforeHidingMarkup(string text)
    {
        var at = text.IndexOf("[private", StringComparison.OrdinalIgnoreCase);
        return at < 0 ? text : text[..at];
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
