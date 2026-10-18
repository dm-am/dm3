using System.Collections.Generic;
using System.Web;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>
/// <c>[spoiler]...[/spoiler]</c> and <c>[spoiler text="..."]...[/spoiler]</c>,
/// whose caption replaces the words on the button.
/// </summary>
/// <remarks>
/// href="#" and not javascript:void(0): a javascript: URL is inline script to
/// CSP, and two decorative ones were the whole reason script-src carried
/// 'unsafe-inline' on a site that binds user HTML through v-html. The client
/// preventDefaults the click on the head, so the href never navigates.
///
/// The author-edit variant is the block alone. The head is for a reader, as the
/// recipients of [private] are: it carries no data-bb-* marker, so the
/// reverse conversion would keep its words as text above the block, one more
/// line of it per edit. The caption rides on the block instead, in its own
/// data-bb-text.
///
/// The words of the open head ride in data-swaptext, and the client shows them
/// while the spoiler is open. A spoiler with a caption keeps it open as well:
/// the caption names what is inside, and "Скрыть содержимое" over an open
/// "Карта сокровищ" dropped the name the moment the reader opened it. One
/// without a caption swaps its words as on DM2.
/// </remarks>
internal sealed class SpoilerTag : Tag
{
    /// <summary>The words on the button of a spoiler that was given no caption.</summary>
    internal const string DefaultCaption = "Показать содержимое";

    /// <summary>The words on the button of an open spoiler that was given no caption.</summary>
    private const string OpenCaption = "Скрыть содержимое";

    private const string Text = "text";

    private static readonly IReadOnlyDictionary<string, ParameterKind> Declared =
        new Dictionary<string, ParameterKind> { [Text] = ParameterKind.Text };

    private readonly bool _authorEdit;

    internal SpoilerTag(bool authorEdit) : base("spoiler", string.Empty, "</div>")
    {
        _authorEdit = authorEdit;
        Parameters = Declared;
        // The reading block ends its line; the author's editor gets every
        // line break back as typed (see BbParserProvider).
        DropsLineBreakAfter = !authorEdit;
    }

    /// <inheritdoc />
    public override string GetOpenHtml(TagNode node, string attributeValue)
    {
        var written = node.Parameters.TryGetValue(Text, out var text);
        var caption = written && text!.Length > 0 ? text : null;

        // The author is given the caption as written, an empty one included:
        // [spoiler text=""] draws what [spoiler] draws, and it is still not
        // what the author typed.
        if (_authorEdit)
        {
            return written
                ? $"<div class=\"spoiler\" data-bb-tag=\"spoiler\" data-bb-text=\"{HttpUtility.HtmlEncode(text)}\">"
                : "<div class=\"spoiler\" data-bb-tag=\"spoiler\">";
        }

        var shown = caption == null ? DefaultCaption : HttpUtility.HtmlEncode(caption);
        var open = caption == null ? OpenCaption : shown;
        return $"<a href=\"#\" class=\"spoiler-head\" data-swaptext=\"{open}\">{shown}</a>" +
               "<div class=\"spoiler\">";
    }
}
