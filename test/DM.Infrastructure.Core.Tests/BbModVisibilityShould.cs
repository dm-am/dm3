using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbVisibilityRender;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// [mod] renders for everyone on read; writing it is gated at save time by ModBlockSanitizer.
/// </summary>
public class BbModVisibilityShould
{
    [Theory]
    [InlineData(BbSurface.Comment, UserRole.Guest)]
    [InlineData(BbSurface.Comment, UserRole.RegularUser)]
    [InlineData(BbSurface.GlobalChatMessage, UserRole.Guest)]
    [InlineData(BbSurface.GlobalChatMessage, UserRole.RegularUser)]
    public void RenderModForEveryoneInAllowedSurfaces(BbSurface surface, UserRole role)
    {
        // [mod] is public on read: even a guest sees the authored mod block.
        var html = Render("[mod]secret[/mod]", surface, Viewer(role));

        html.Should().Contain("mod-block");
        html.Should().Contain("secret");
    }

    [Theory]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void RenderModForModeratorPlusInForum(UserRole role)
    {
        var html = Render("[mod]secret[/mod]", BbSurface.Comment, Viewer(role));

        html.Should().Contain("mod-block");
        html.Should().Contain("secret");
    }

    [Fact]
    public void RenderModInPlainTextAudienceKeepsInnerText()
    {
        // [mod] is no longer privacy-sensitive: its inner text renders in
        // plain text like any normal formatting tag (not stripped).
        var text = RenderText("before [mod]secret[/mod] after",
            BbSurface.Comment,
            audience: RenderAudience.PlainText);

        text.Should().Contain("secret");
        text.Should().Contain("before");
        text.Should().Contain("after");
    }
}
