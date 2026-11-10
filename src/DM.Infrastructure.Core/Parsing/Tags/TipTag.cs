using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BBCodeParser;
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
/// A span carrying what it shows in two attributes, the contract with the client,
/// held by shared cases on both sides. The picture is not in the markup: the
/// tooltip loads it when opened. A word with markup or a line break in it, or a
/// tag that shows nothing or names a refused address, is printed as typed.
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
        string.IsNullOrWhiteSpace(raw) || raw.AsSpan().IndexOfAny('\r', '\n') >= 0 ? null : raw;

    /// <inheritdoc />
    public override string RenderHtml(TagNode node, string content)
    {
        if (!TryRead(node, out var text, out var image))
        {
            return EncodeText(AsTyped(node, content));
        }

        var html = new StringBuilder("<span class=\"bb-tip\" tabindex=\"0\"");
        if (text != null) html.Append(" data-tip-text=\"").Append(EncodeAttribute(text)).Append('"');
        if (image != null) html.Append(" data-tip-img=\"").Append(EncodeAttribute(image)).Append('"');
        return html.Append('>').Append(EncodeText(content)).Append("</span>").ToString();
    }

    /// <inheritdoc />
    public override string RenderText(TagNode node, string content) =>
        TryRead(node, out _, out _) ? content : AsTyped(node, content);

    private static string AsTyped(TagNode node, string content) =>
        (node.OpeningText ?? "[tip]") + content + (node.ClosingText ?? "[/tip]");

    /// <summary>
    /// What the tag shows: a text with a character in it besides white space,
    /// an address the rule accepts, or both. False when it shows nothing, or
    /// when the address it was given is refused.
    /// </summary>
    private static bool TryRead(TagNode node, out string? text, out string? image)
    {
        text = node.Parameters.TryGetValue(Text, out var written) && !string.IsNullOrWhiteSpace(written)
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

    private static string EncodeText(string value) =>
        BbParser.SecuritySubstitutions.Aggregate(value, (text, pair) => text.Replace(pair.Key, pair.Value));

    /// <summary>
    /// A value inside a quoted attribute. Not HttpUtility, whose set differs from
    /// the client's, which draws the same span for its preview.
    /// </summary>
    private static string EncodeAttribute(string value) =>
        EncodeText(value).Replace("\"", "&quot;").Replace("'", "&#39;");
}
