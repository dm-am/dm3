using System.Collections.Generic;
using System.Web;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>
/// <c>[link]address[/link]</c>, which shows the word "ссылка" as on DM2, and
/// <c>[link text="..."]address[/link]</c>, which shows its own text.
/// </summary>
/// <remarks>
/// The text is a parameter of a node like any other, so it is read by the
/// grammar and encoded where it is written. It used to be cut out of the post by
/// a regular expression before the parse, as the attribute of a spelling that
/// no longer exists, and every rule that had to hold for it had to be repeated
/// for that expression.
/// </remarks>
internal sealed class LinkTag : ContentTag
{
    /// <summary>What a link shows when it was given no text, as on DM2.</summary>
    internal const string DefaultText = "ссылка";

    private const string Text = "text";

    private static readonly IReadOnlyDictionary<string, ParameterKind> Declared =
        new Dictionary<string, ParameterKind> { [Text] = ParameterKind.Text };

    internal LinkTag() : base("link", Declared)
    {
    }

    /// <inheritdoc />
    public override string? ReadContent(string raw) => BbAddress.Read(raw);

    /// <inheritdoc />
    /// <remarks>
    /// The content is an address <see cref="BbAddress.Read"/> accepted: one it
    /// refused makes the tag the text that was typed, and this is never asked.
    /// </remarks>
    public override string RenderHtml(TagNode node, string content) =>
        $"<a href=\"{HttpUtility.HtmlAttributeEncode(content)}\" target=\"_blank\" rel=\"noopener\">" +
        $"{HttpUtility.HtmlEncode(OwnText(node) ?? DefaultText)}</a>";

    /// <inheritdoc />
    public override string RenderText(TagNode node, string content) => OwnText(node) ?? content;

    /// <summary>The text the author gave the link, or null when there is none.</summary>
    private static string? OwnText(TagNode node) =>
        node.Parameters.TryGetValue(Text, out var text) && text.Length > 0 ? text : null;
}
