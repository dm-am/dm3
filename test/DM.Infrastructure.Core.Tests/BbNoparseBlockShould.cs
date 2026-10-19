using AwesomeAssertions;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbAudienceRender;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The [noparse] block shows markup as typed, and only inside it
/// (BBCODE_RENDERING.md, "[code] и [noparse]").
/// </summary>
public class BbNoparseBlockShould
{
    /// <summary>
    /// [noparse] shows markup, it does not run it. [code] is no such block: it
    /// changes the font, and what the grammar table says of it is the rule.
    /// </summary>
    [Theory]
    [InlineData("[noparse][img]https://example.com/a.png[/img][/noparse]")]
    [InlineData("[noparse][link text=\"text\"]https://example.com[/link][/noparse]")]
    [InlineData("[noparse][mention]Вася[/mention][/noparse]")]
    public void ShowMarkupInsideVerbatimBlocksRatherThanRenderIt(string input)
    {
        Comment(input).Should().NotContain("<img").And.NotContain("<a ");
    }

    /// <summary>
    /// The counterweight: outside the block the same tags still render.
    /// </summary>
    [Fact]
    public void KeepRenderingTheSameTagsOutsideAVerbatimBlock()
    {
        var html = Comment(
            "[noparse][img]https://example.com/a.png[/img][/noparse][img]https://example.com/b.png[/img]");

        html.Should().Contain("<img src=\"https://example.com/b.png\"");
        html.Should().NotContain("<img src=\"https://example.com/a.png\"");
    }
}
