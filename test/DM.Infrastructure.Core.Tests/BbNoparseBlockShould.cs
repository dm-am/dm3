using AwesomeAssertions;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbAudienceRender;

namespace DM.Infrastructure.Core.Tests;

public class BbNoparseBlockShould
{
    // [code] is no such block: it changes the font, and what the grammar table says of it is the
    // rule.
    [Theory]
    [InlineData("[noparse][img]https://example.com/a.png[/img][/noparse]")]
    [InlineData("[noparse][link text=\"text\"]https://example.com[/link][/noparse]")]
    [InlineData("[noparse][mention]Вася[/mention][/noparse]")]
    public void ShowMarkupInsideVerbatimBlocksRatherThanRenderIt(string input)
    {
        Comment(input).Should().NotContain("<img").And.NotContain("<a ");
    }

    [Fact]
    public void KeepRenderingTheSameTagsOutsideAVerbatimBlock()
    {
        var html = Comment(
            "[noparse][img]https://example.com/a.png[/img][/noparse][img]https://example.com/b.png[/img]");

        html.Should().Contain("<img src=\"https://example.com/b.png\"");
        html.Should().NotContain("<img src=\"https://example.com/a.png\"");
    }
}
