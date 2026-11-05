using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The words the page draws on a spoiler, a [nsfw] block and a link without a
/// text are the words the editor's preview, its help and the client's toggle draw.
/// </summary>
/// <remarks>
/// The server renders the page, the client renders the preview and swaps the
/// words of an open block, so both spell them. The client keeps them in one file,
/// bbcodeConstants.ts, read here and held to what the server renders.
/// </remarks>
public class MarkupCaptionsShould
{
    private const string ConstantsFile = "shared/lib/bbcode/bbcodeConstants.ts";

    private static readonly BbParserWrapper Page = new BbParserProvider().GetForSurface(BbSurface.Comment);

    [Fact]
    public void CaptionASpoilerWithTheClientsWords() =>
        Render("[spoiler]текст[/spoiler]").Should().Contain(
            $"data-swaptext=\"{Text("SPOILER_HIDE_TEXT")}\">{Text("SPOILER_SHOW_TEXT")}</a>");

    [Fact]
    public void CaptionAnNsfwBlockWithTheClientsWords() =>
        Render("[nsfw]текст[/nsfw]").Should().Contain(
            $"data-swaptext=\"{Text("NSFW_HIDE_TEXT")}\">{Text("NSFW_SHOW_TEXT")}</a>");

    [Fact]
    public void NameALinkWithoutATextWithTheClientsWord() =>
        Render("[link]https://example.com/[/link]").Should().EndWith($">{Text("DEFAULT_LINK_TEXT")}</a>");

    private static string Render(string bbCode) =>
        Page.RenderHtml(bbCode, new RenderContext { Audience = RenderAudience.Display, Surface = BbSurface.Comment });

    /// <summary>The string a client constant is declared with, on its line or the next.</summary>
    private static string Text(string constant)
    {
        var match = Regex.Match(ClientCopies.Source(ConstantsFile),
            @"\bexport const " + Regex.Escape(constant) + @"\s*=\s*""(?<value>[^""]*)"";");
        match.Success.Should().BeTrue($"{ConstantsFile} declares {constant} as a string");
        return match.Groups["value"].Value;
    }
}
