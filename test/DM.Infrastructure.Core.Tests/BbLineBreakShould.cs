using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbAudienceRender;

namespace DM.Infrastructure.Core.Tests;

public class BbLineBreakShould
{
    // [code] keeps its breaks (pre-wrap), and the browser turns every CR into LF.
    [Theory]
    [InlineData("[code]раз\r\nдва[/code]", "[code]раз\nдва[/code]")]
    [InlineData("раз\r\nдва", "раз\nдва")]
    public void DrawOneBreakForALineBreakTypedAsCrLf(string typed, string withLineFeed) =>
        Comment(typed).Should().Be(Comment(withLineFeed));

    // The page collapses white space, so the break is the renderer's to draw.
    [Theory]
    [InlineData(BbSurface.DirectMessage)]
    [InlineData(BbSurface.GamePost)]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.GlobalChatMessage)]
    [InlineData(BbSurface.Profile)]
    public void DrawTheLineBreakOnEverySurface(BbSurface surface)
    {
        Html(surface, "раз\nдва").Should().Be("раз<br />два");
    }
}
