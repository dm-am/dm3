using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The [quote] tag is formatting: a block around the text, with nothing of its
/// own inside.
/// </summary>
/// <remarks>The tag carries no attribute, as on DM2: one makes it a broken tag shown as typed.</remarks>
public class BbQuoteBlockShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private string ToHtml(BbSurface surface, string bbCode) =>
        _parserProvider.GetForSurface(surface)
        .RenderHtml(bbCode, new RenderContext { Audience = RenderAudience.Display, Surface = surface });

    [Theory]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.GamePost)]
    [InlineData(BbSurface.GlobalChatMessage)]
    [InlineData(BbSurface.Profile)]
    [InlineData(BbSurface.DirectMessage)]
    public void RenderAsABlockWithoutAHeader(BbSurface surface) =>
        ToHtml(surface, "[quote]текст[/quote]").Should().Be("<div class=\"quote\">текст</div>");

    [Theory]
    [InlineData("quote")]
    [InlineData("spoiler")]
    [InlineData("b")]
    public void ShowAQuotedValueAsTextAsAnyTagWithoutAnAttributeDoes(string tagName) =>
        ToHtml(BbSurface.Comment, $"[{tagName}=\"Вася\"]текст[/{tagName}]")
            .Should().Be($"[{tagName}=\"Вася\"]текст[/{tagName}]");

    [Theory]
    [InlineData("quote")]
    [InlineData("spoiler")]
    [InlineData("b")]
    public void ShowAnUnquotedValueAsTextAsAnyTagWithoutAnAttributeDoes(string tagName) =>
        ToHtml(BbSurface.Comment, $"[{tagName}=Вася]текст[/{tagName}]")
            .Should().Be($"[{tagName}=Вася]текст[/{tagName}]");

    /// <summary>
    /// The content of a broken quote is not text: it renders, bold stays bold.
    /// </summary>
    [Fact]
    public void RenderTheContentOfABrokenQuote() =>
        ToHtml(BbSurface.Comment, "[quote=\"Вася\"][b]текст[/b][/quote]")
            .Should().Be("[quote=\"Вася\"]<strong>текст</strong>[/quote]");
}
