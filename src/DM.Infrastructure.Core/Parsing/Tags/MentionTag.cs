using System;
using System.Collections.Generic;
using System.Web;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>
/// <c>[mention]Login[/mention]</c>: a link to the profile of the user, drawn as
/// "@Login".
/// </summary>
/// <remarks>
/// It is in the tag set of every surface, and every audience renders it the
/// same way: its markup carries <c>data-bb-tag</c> and <c>data-bb-user</c>, which
/// is all the author's editor needs to write the tag back. Whether the user
/// exists is not asked, and nobody is notified.
/// </remarks>
internal sealed class MentionTag : ContentTag
{
    internal MentionTag() : base("mention", new Dictionary<string, ParameterKind>())
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
