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
/// href="#" and not javascript:, which CSP counts as inline script; the client
/// cancels the click. A spoiler with a caption keeps it when open, since it names
/// what is inside; one without swaps its words (data-swaptext) as on DM2.
/// </remarks>
internal sealed class SpoilerTag : Tag
{
    /// <summary>The words on the button of a spoiler that was given no caption.</summary>
    private const string DefaultCaption = "Показать содержимое";

    /// <summary>The words on the button of an open spoiler that was given no caption.</summary>
    private const string OpenCaption = "Скрыть содержимое";

    private const string Text = "text";

    private static readonly IReadOnlyDictionary<string, ParameterKind> Declared =
        new Dictionary<string, ParameterKind> { [Text] = ParameterKind.Text };

    internal SpoilerTag() : base("spoiler", string.Empty, "</div>")
    {
        Parameters = Declared;
        DropsLineBreakAfter = true;
    }

    /// <inheritdoc />
    public override string GetOpenHtml(TagNode node, string attributeValue)
    {
        var caption = node.Parameters.TryGetValue(Text, out var text) && text!.Length > 0 ? text : null;
        return Open(caption == null ? null : HttpUtility.HtmlEncode(caption));
    }

    /// <summary>
    /// The head of a spoiler and the opening of its body: the one markup of every
    /// spoiler, the one an author writes and the one the site puts an image behind,
    /// so both open and close alike and say the same words.
    /// </summary>
    /// <param name="caption">The caption the author gave, encoded; null for none.</param>
    /// <param name="mark">
    /// An attribute both elements carry, with its leading space, telling a spoiler
    /// the site put up from one an author wrote; empty for the author's.
    /// </param>
    internal static string Open(string? caption, string mark = "")
    {
        var shown = caption ?? DefaultCaption;
        var open = caption ?? OpenCaption;
        return $"<a href=\"#\" class=\"spoiler-head\"{mark} data-swaptext=\"{open}\">{shown}</a>" +
               $"<div class=\"spoiler\"{mark}>";
    }
}
