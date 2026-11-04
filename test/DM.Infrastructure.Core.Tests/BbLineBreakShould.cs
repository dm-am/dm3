using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbAudienceRender;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// A line break the author typed: one break on the page, on every surface, and
/// the same break read back by the author's editor.
/// </summary>
public class BbLineBreakShould
{
    /// <summary>
    /// A line break typed as CR LF is one break on the page, in [code] as well.
    /// </summary>
    /// <remarks>[code] keeps its breaks (pre-wrap), and the browser turns every CR into LF.</remarks>
    [Theory]
    [InlineData("[code]раз\r\nдва[/code]", "[code]раз\nдва[/code]")]
    [InlineData("раз\r\nдва", "раз\nдва")]
    public void DrawOneBreakForALineBreakTypedAsCrLf(string typed, string withLineFeed) =>
        Comment(typed).Should().Be(Comment(withLineFeed));

    /// <summary>
    /// A message written on two lines is shown on two lines, private messages
    /// included.
    /// </summary>
    /// <remarks>The page collapses white space, so the break is the renderer's to draw.</remarks>
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
