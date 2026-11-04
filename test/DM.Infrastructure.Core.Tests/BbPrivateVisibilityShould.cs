using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbVisibilityRender;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// [private] visibility — game posts only: the author, the addressee, the
/// leads, the settings that share private text, and every spelling of the
/// block.
/// </summary>
public class BbPrivateVisibilityShould
{
    [Fact]
    public void RenderPrivateForAuthorAlwaysVisible()
    {
        var viewer = Viewer(UserRole.RegularUser, AuthorId);
        var ctx = DisplayCtxForGamePost(viewer);
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    [Fact]
    public void RenderPrivateForAddresseeOwner()
    {
        var viewer = Viewer(UserRole.RegularUser, OwnerB);
        var ctx = AddressedCtx(viewer, "B", OwnerB);
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    [Fact]
    public void StripPrivateForNonAddresseeNonAuthorNonLead()
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var ctx = AddressedCtx(viewer, "B", OwnerB);
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
        html.Should().NotContain("private-message");
    }

    /// <summary>
    /// Every spelling of the block is a block, and none of them reaches a reader
    /// the block was not addressed to.
    /// </summary>
    /// <remarks>Only the quoted form addresses anybody; the rest address nobody, and a stranger sees none.</remarks>
    [Theory]
    [InlineData("[private=B]secret[/private]")]
    [InlineData("[private=\"B\"]secret[/private]")]
    [InlineData("[PRIVATE=\"B\"]secret[/PRIVATE]")]
    [InlineData("[Private=B]secret[/Private]")]
    [InlineData("[private=\"B]secret[/private]")]
    [InlineData("[private=B\"]secret[/private]")]
    public void StripPrivateForNonAddresseeWhicheverWayTheAttributeIsWritten(string input)
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var ctx = AddressedCtx(viewer, "B", OwnerB);

        var html = RenderWithContext(input, BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
        html.Should().NotContain("private-message");
    }

    /// <summary>
    /// The snapshot is keyed by the name as the author typed it, and the render
    /// path encodes that name before the parser sees it.
    /// </summary>
    /// <remarks>HtmlEncode covers the apostrophe too; asserted both ways, as the comparison decides visibility.</remarks>
    [Theory]
    [InlineData("D'Artagnan")]
    [InlineData("Tom & Jerry")]
    [InlineData("A <B>")]
    public void RenderPrivateForAddresseeOwnerWhenTheNameCarriesHtmlSyntax(string name)
    {
        var viewer = Viewer(UserRole.RegularUser, OwnerB);
        var ctx = AddressedCtx(viewer, name, OwnerB);

        var html = RenderWithContext($"[private=\"{name}\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    [Theory]
    [InlineData("D'Artagnan")]
    [InlineData("Tom & Jerry")]
    [InlineData("A <B>")]
    public void StripPrivateForNonAddresseeWhenTheNameCarriesHtmlSyntax(string name)
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var ctx = AddressedCtx(viewer, name, OwnerB);

        var html = RenderWithContext($"[private=\"{name}\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
        html.Should().NotContain("private-message");
    }

    [Fact]
    public void RenderPrivateForGameLead()
    {
        // Master sees every [private] in the game regardless of addressees.
        var viewer = Viewer(UserRole.RegularUser, MasterId);
        var ctx = DisplayCtxForGamePost(viewer) with
        {
            GameLeadUserIds = new[] { MasterId }
        };
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    [Fact]
    public void StripPrivateForPlainModeratorNoBypass()
    {
        // Moderator+ does NOT see [private] without being a lead / addressee.
        var viewer = Viewer(UserRole.Moderator, OtherUser);
        var ctx = DisplayCtxForGamePost(viewer);
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
    }

    [Fact]
    public void RenderPrivateWhenGameShowPrivateMessages()
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var ctx = DisplayCtxForGamePost(viewer) with { GameShowPrivateMessages = true };
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    [Fact]
    public void RenderPrivateWhenRoomViewPrivateText()
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var ctx = DisplayCtxForGamePost(viewer) with { RoomViewPrivateText = true };
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    /// <summary>
    /// The two settings share text addressed to characters; a bare or misspelled block addresses nobody and
    /// stays with the author and the leads.
    /// </summary>
    [Theory]
    [InlineData("[private]secret[/private]", true, false)]
    [InlineData("[private]secret[/private]", false, true)]
    [InlineData("[private=\"\"]secret[/private]", true, false)]
    [InlineData("[private=B]secret[/private]", true, false)]
    [InlineData("[private to=\"B\"]secret[/private]", false, true)]
    public void KeepABlockThatAddressesNobodyFromTheSettingsThatShareAddressedText(
        string input, bool shareWithAll, bool roomShowsPrivateText)
    {
        var ctx = DisplayCtxForGamePost(Viewer(UserRole.RegularUser, OtherUser)) with
        {
            GameShowPrivateMessages = shareWithAll,
            RoomViewPrivateText = roomShowsPrivateText,
            GameLeadUserIds = [MasterId]
        };

        RenderWithContext(input, BbSurface.GamePost, ctx).Should().NotContain("secret");
        RenderWithContext(input, BbSurface.GamePost, ctx with { Viewer = Viewer(UserRole.RegularUser, MasterId) })
            .Should().Contain("secret");
        RenderWithContext(input, BbSurface.GamePost, ctx with { Viewer = Viewer(UserRole.RegularUser, AuthorId) })
            .Should().Contain("secret");
    }

    /// <summary>
    /// A misspelled block is not read for its name, even when the snapshot holds
    /// an entry under the text it seems to carry.
    /// </summary>
    [Theory]
    [InlineData("[private=B]secret[/private]")]
    [InlineData("[private=\"B]secret[/private]")]
    [InlineData("[PRIVATE to=\"B\"]secret[/private]")]
    public void HideAMisspelledBlockFromTheOwnerOfTheNameItSeemsToCarry(string input)
    {
        var ctx = AddressedCtx(Viewer(UserRole.RegularUser, OwnerB), "B", OwnerB);

        RenderWithContext(input, BbSurface.GamePost, ctx).Should().NotContain("secret");
    }

    /// <summary>
    /// A name in capitals is the name: the block is well spelled, and it reaches
    /// the reader it names.
    /// </summary>
    [Theory]
    [InlineData("[PRIVATE=\"B\"]secret[/PRIVATE]")]
    [InlineData("[Private=\"B\"]secret[/private]")]
    public void ShowABlockSpelledInCapitalsToTheOwnerOfTheNameItCarries(string input)
    {
        var ctx = AddressedCtx(Viewer(UserRole.RegularUser, OwnerB), "B", OwnerB);

        RenderWithContext(input, BbSurface.GamePost, ctx).Should().Contain("secret");
        RenderWithContext(input, BbSurface.GamePost, ctx with { Viewer = Viewer(UserRole.RegularUser, OtherUser) })
            .Should().NotContain("secret");
    }

    /// <summary>
    /// Off the game post a block is read and erased for every reader, its author
    /// and the game's leads included.
    /// </summary>
    [Theory]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.GlobalChatMessage)]
    [InlineData(BbSurface.Profile)]
    [InlineData(BbSurface.DirectMessage)]
    public void StripPrivateForEveryReaderOffTheGamePost(BbSurface surface)
    {
        var ctx = AddressedCtx(Viewer(UserRole.RegularUser, AuthorId), "B", OwnerB) with
        {
            Surface = surface,
            GameLeadUserIds = [MasterId],
            GameShowPrivateMessages = true,
            RoomViewPrivateText = true
        };

        foreach (var reader in new[] { AuthorId, MasterId, OwnerB, OtherUser })
        {
            var html = RenderWithContext("before [private=\"B\"]secret[/private] after", surface,
                ctx with { Viewer = Viewer(UserRole.Admin, reader) });

            html.Should().Be("before  after");
        }
    }

    [Fact]
    public void StripPrivateInPlainTextAudience()
    {
        var text = RenderText("before [private=\"B\"]secret[/private] after",
            BbSurface.GamePost,
            audience: RenderAudience.PlainText);

        text.Should().NotContain("secret");
        text.Should().Contain("before");
        text.Should().Contain("after");
    }

    /// <summary>
    /// A tag the author forgot to close does not carry the block past the filter.
    /// </summary>
    /// <remarks>
    /// Swallowed text never becomes a node the filter is asked about. Both audiences: the plain text is what
    /// the stored search text and X-Dm-Audience: plain_text return.
    /// </remarks>
    [Theory]
    [InlineData("до [img]https://example.com/a.png\n[private=\"B\"]secret[/private]\n" +
                "[img]https://example.com/b.png[/img] после")]
    [InlineData("до [img]https://example.com/a.png[private=\"B\"]secret[/private]" +
                "[img]https://example.com/b.png[/img] после")]
    [InlineData("до [link]https://example.com/a\n[private=\"B\"]secret[/private]\n" +
                "[link]https://example.com/b[/link] после")]
    [InlineData("до [img width=\"100\" height=\"50\"]https://example.com/a.png\n[private=\"B\"]secret[/private]\n" +
                "[img width=\"100\" height=\"60\"]https://example.com/b.png[/img] после")]
    public void StripPrivateWhenAnUnclosedTagAboveItReachesForTheNextClosingTag(string input)
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var ctx = DisplayCtxForGamePost(viewer);

        var html = RenderWithContext(input, BbSurface.GamePost, ctx);
        var text = RenderText(input, BbSurface.GamePost, RenderAudience.PlainText);

        html.Should().NotContain("secret");
        text.Should().NotContain("secret");

        // And the author keeps their post: the words on either side of the tag
        // are still there, which is the half of this that failed silently.
        html.Should().Contain("до").And.Contain("после");
        text.Should().Contain("до").And.Contain("после");
    }
}
