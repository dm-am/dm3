using System;
using System.Collections.ObjectModel;
using System.Web;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>
/// <c>[mention]Login[/mention]</c>: a link to the profile of the user, drawn as
/// "@Login".
/// </summary>
/// <remarks>
/// The same for every audience, and the markup the editor's own preview draws
/// (bbcodeGrammar.cases.ts): data-bb-tag and data-bb-user are what the editor
/// reads back from that preview. Whether the user exists is not asked, and nobody
/// is notified.
/// </remarks>
internal sealed class MentionTag : ContentTag
{
    internal MentionTag() : base("mention", ReadOnlyDictionary<string, ParameterKind>.Empty)
    {
    }

    /// <summary>
    /// A login is text that is not empty and carries neither an opening bracket
    /// nor a line break; the whitespace around it is not part of it.
    /// </summary>
    public override string? ReadContent(string raw)
    {
        var login = raw.Trim();
        return login.Length == 0 || login.AsSpan().IndexOfAny('[', '\n', '\r') >= 0 ? null : login;
    }

    /// <inheritdoc />
    public override string RenderHtml(TagNode node, string content) =>
        $"<a class=\"bb-mention\" href=\"/users/{Uri.EscapeDataString(content)}\" data-bb-tag=\"mention\" " +
        $"data-bb-user=\"{HttpUtility.HtmlAttributeEncode(content)}\">@{HttpUtility.HtmlEncode(content)}</a>";

    /// <inheritdoc />
    public override string RenderText(TagNode node, string content) => $"@{content}";
}
