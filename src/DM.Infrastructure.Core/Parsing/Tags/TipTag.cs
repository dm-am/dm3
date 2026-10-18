using System;
using System.Collections.Generic;
using System.Text;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>
/// <c>[tip text="..."]word[/tip]</c> and <c>[tip img="address"]word[/tip]</c>,
/// the one tag of a tag description (<see cref="BbSurface.Hint"/>): a word that
/// shows a text, a picture, or a picture with the text under it when a reader
/// points at it or taps it.
/// </summary>
/// <remarks>
/// The word is drawn as a span that carries what it shows in two attributes,
/// and the client draws the small tooltip over the word from them. The picture
/// is not in the markup: the tooltip loads it when a reader opens it, not every
/// page that lists tags. The attribute names are the contract with the client,
/// held by the pairs of hintMarkup.cases.ts on both sides.
///
/// The word is one line of text: markup inside it, a [tip] inside another
/// among it, makes the tag the text that was typed, as it does for [link], and
/// so does a line break. A tag that shows nothing - neither a text with a
/// character in it nor a picture - is text as typed too, and so is one whose
/// address is refused: the address follows the rule of [img], and besides it a
/// path on this site, "/images/a.gif", written with one slash (see
/// <see cref="BbAddress.ReadTipImage"/>).
///
/// Written as typed, the tag is its opening, its word and its closing, encoded
/// the way the parser encodes text. That is what the parser prints for a tag it
/// did not accept, for this surface: the word holds no line break and the hint
/// has no typography, so the text of the word is the text a reader gets.
/// </remarks>
internal sealed class TipTag : ContentTag
{
    private const string Text = "text";
    private const string Image = "img";

    private static readonly IReadOnlyDictionary<string, ParameterKind> Declared =
        new Dictionary<string, ParameterKind>
        {
            [Text] = ParameterKind.Text,
            [Image] = ParameterKind.Text
        };

    internal TipTag() : base("tip", Declared)
    {
    }

    /// <summary>
    /// The word: text with a character in it besides white space, on one line.
    /// </summary>
    public override string? ReadContent(string raw) =>
        raw.Trim().Length == 0 || raw.AsSpan().IndexOfAny('\r', '\n') >= 0 ? null : raw;

    /// <inheritdoc />
    public override string RenderHtml(TagNode node, string content)
    {
        if (!TryRead(node, out var text, out var image))
        {
            return EncodeText(node.OpeningText ?? "[tip]") + EncodeText(content) +
                   EncodeText(node.ClosingText ?? "[/tip]");
        }

        var html = new StringBuilder("<span class=\"bb-tip\" tabindex=\"0\"");
        if (text != null) html.Append(" data-tip-text=\"").Append(EncodeAttribute(text)).Append('"');
        if (image != null) html.Append(" data-tip-img=\"").Append(EncodeAttribute(image)).Append('"');
        return html.Append('>').Append(EncodeText(content)).Append("</span>").ToString();
    }

    /// <inheritdoc />
    public override string RenderText(TagNode node, string content) =>
        TryRead(node, out _, out _)
            ? content
            : (node.OpeningText ?? "[tip]") + content + (node.ClosingText ?? "[/tip]");

    /// <summary>
    /// What the tag shows: a text with a character in it besides white space,
    /// an address the rule accepts, or both. False when it shows nothing, or
    /// when the address it was given is refused.
    /// </summary>
    private static bool TryRead(TagNode node, out string? text, out string? image)
    {
        text = node.Parameters.TryGetValue(Text, out var written) && written.Trim().Length > 0
            ? written
            : null;
        image = null;

        if (node.Parameters.TryGetValue(Image, out var address))
        {
            image = BbAddress.ReadTipImage(address);
            if (image == null) return false;
        }

        return text != null || image != null;
    }

    /// <summary>Text encoded the way the parser encodes text: ampersand and angle brackets.</summary>
    private static string EncodeText(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>
    /// A value inside a quoted attribute: the text encoding and both quotes.
    /// Written out rather than taken from HttpUtility, whose set differs from
    /// the client's and moves with the framework: the client draws the same
    /// span for its preview, and the pairs hold the two to one string.
    /// </summary>
    private static string EncodeAttribute(string value) =>
        EncodeText(value).Replace("\"", "&quot;").Replace("'", "&#39;");
}
