using System;
using System.Collections.Generic;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Core.Parsing.Visitors;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Covers the visibility matrix declared in the 3B plan: every row of
/// (tag × audience × role × context) must produce the expected output.
/// The visitor is a pure function; these tests never hit the DI container.
/// </summary>
public class PermissionFilteringVisitorShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MasterId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OwnerB = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherUser = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid GameId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // ════════════════════════════════════════════════════════════════
    // [mod] visibility — public on read: renders for EVERYONE on the
    // comment / global chat surfaces (write access is gated elsewhere,
    // at save time by ModBlockSanitizer).
    // ════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(BbSurface.Comment, UserRole.Guest)]
    [InlineData(BbSurface.Comment, UserRole.RegularUser)]
    [InlineData(BbSurface.GlobalChatMessage, UserRole.Guest)]
    [InlineData(BbSurface.GlobalChatMessage, UserRole.RegularUser)]
    public void RenderMod_ForEveryone_InAllowedSurfaces(BbSurface surface, UserRole role)
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
    public void RenderMod_ForModeratorPlus_InForum(UserRole role)
    {
        var html = Render("[mod]secret[/mod]", BbSurface.Comment, Viewer(role));

        html.Should().Contain("mod-block");
        html.Should().Contain("secret");
    }

    [Fact]
    public void RenderMod_InPlainTextAudience_KeepsInnerText()
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

    [Fact]
    public void RenderMod_WithRoundTripAttributes_InAuthorEdit()
    {
        var author = Viewer(UserRole.Moderator, AuthorId);
        var ctx = RenderContext.ForAuthorEdit(author, BbSurface.Comment);
        var html = RenderWithContext("[mod]secret[/mod]", BbSurface.Comment, ctx);

        html.Should().Contain("data-bb-tag=\"mod\"");
        html.Should().Contain("secret");
    }

    // ════════════════════════════════════════════════════════════════
    // [private] visibility — game posts only.
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void RenderPrivate_ForAuthor_AlwaysVisible()
    {
        var viewer = Viewer(UserRole.RegularUser, AuthorId);
        var ctx = DisplayCtxForGamePost(viewer);
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    [Fact]
    public void RenderPrivate_ForAddresseeOwner()
    {
        var viewer = Viewer(UserRole.RegularUser, OwnerB);
        var addressees = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal)
        {
            ["B"] = new HashSet<Guid> { OwnerB }
        };
        var ctx = DisplayCtxForGamePost(viewer) with
        {
            PrivateAddresseeOwnerUserIdsByAttribute = addressees
        };
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    [Fact]
    public void StripPrivate_ForNonAddressee_NonAuthor_NonLead()
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var addressees = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal)
        {
            ["B"] = new HashSet<Guid> { OwnerB }
        };
        var ctx = DisplayCtxForGamePost(viewer) with
        {
            PrivateAddresseeOwnerUserIdsByAttribute = addressees
        };
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
        html.Should().NotContain("private-message");
    }

    /// <summary>
    /// The unquoted attribute form, which is the only one the product produces.
    /// </summary>
    /// <remarks>
    /// Every other test in this class writes [private="B"], and the editor writes
    /// [private=B]: bbcode.ts builds the tag without quotes, the toolbar wraps a
    /// selection without quotes, and the editor's own help text teaches the
    /// unquoted form. So both halves of the contract were covered — on different
    /// syntaxes — and nothing covered the syntax that actually travels.
    ///
    /// Theory rather than two facts so the pair stays visibly symmetrical: if the
    /// parser ever stops accepting one of them, the row says which.
    ///
    /// The one-sided rows are the same leak through the spelling nobody checked:
    /// a leading quote used to be read as "already quoted" and passed through, so
    /// the parser refused the unterminated attribute and the block was served as
    /// plain text to every reader of the room.
    /// </remarks>
    [Theory]
    [InlineData("[private=B]secret[/private]")]
    [InlineData("[private=\"B\"]secret[/private]")]
    [InlineData("[PRIVATE=\"B\"]secret[/PRIVATE]")]
    [InlineData("[Private=B]secret[/Private]")]
    [InlineData("[private=\"B]secret[/private]")]
    [InlineData("[private=B\"]secret[/private]")]
    public void StripPrivate_ForNonAddressee_WhicheverWayTheAttributeIsWritten(string input)
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var addressees = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal)
        {
            ["B"] = new HashSet<Guid> { OwnerB }
        };
        var ctx = DisplayCtxForGamePost(viewer) with
        {
            PrivateAddresseeOwnerUserIdsByAttribute = addressees
        };

        var html = RenderWithContext(input, BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
        html.Should().NotContain("private-message");
    }

    /// <summary>
    /// The snapshot is keyed by the name as the author typed it, and the render
    /// path encodes that name before the parser sees it.
    /// </summary>
    /// <remarks>
    /// Not an exotic input: HtmlEncode covers the apostrophe alongside the
    /// ampersand and the brackets, so a plain character name was enough. Both
    /// directions are asserted, because the fix moves a comparison that decides
    /// visibility: the owner has to see the block, and a reader who qualifies
    /// through no rule still must not.
    /// </remarks>
    [Theory]
    [InlineData("D'Artagnan")]
    [InlineData("Tom & Jerry")]
    [InlineData("A <B>")]
    public void RenderPrivate_ForAddresseeOwner_WhenTheNameCarriesHtmlSyntax(string name)
    {
        var viewer = Viewer(UserRole.RegularUser, OwnerB);
        var addressees = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal)
        {
            [name] = new HashSet<Guid> { OwnerB }
        };
        var ctx = DisplayCtxForGamePost(viewer) with
        {
            PrivateAddresseeOwnerUserIdsByAttribute = addressees
        };

        var html = RenderWithContext($"[private=\"{name}\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    [Theory]
    [InlineData("D'Artagnan")]
    [InlineData("Tom & Jerry")]
    [InlineData("A <B>")]
    public void StripPrivate_ForNonAddressee_WhenTheNameCarriesHtmlSyntax(string name)
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var addressees = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal)
        {
            [name] = new HashSet<Guid> { OwnerB }
        };
        var ctx = DisplayCtxForGamePost(viewer) with
        {
            PrivateAddresseeOwnerUserIdsByAttribute = addressees
        };

        var html = RenderWithContext($"[private=\"{name}\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
        html.Should().NotContain("private-message");
    }

    [Fact]
    public void RenderPrivate_ForGameLead()
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
    public void StripPrivate_ForPlainModerator_NoBypass()
    {
        // Moderator+ does NOT see [private] without being a lead / addressee.
        var viewer = Viewer(UserRole.Moderator, OtherUser);
        var ctx = DisplayCtxForGamePost(viewer);
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
    }

    [Fact]
    public void RenderPrivate_WhenPostSharePrivateWithAll()
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var ctx = DisplayCtxForGamePost(viewer) with { PostSharePrivateWithAll = true };
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    [Fact]
    public void RenderPrivate_WhenRoomViewPrivateText()
    {
        var viewer = Viewer(UserRole.RegularUser, OtherUser);
        var ctx = DisplayCtxForGamePost(viewer) with { RoomViewPrivateText = true };
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("secret");
    }

    [Fact]
    public void StripPrivate_InPlainTextAudience()
    {
        var text = RenderText("before [private=\"B\"]secret[/private] after",
            BbSurface.GamePost,
            audience: RenderAudience.PlainText);

        text.Should().NotContain("secret");
        text.Should().Contain("before");
        text.Should().Contain("after");
    }

    [Fact]
    public void RenderPrivate_WithRoundTripAttributes_InAuthorEdit()
    {
        var author = Viewer(UserRole.RegularUser, AuthorId);
        var ctx = RenderContext.ForAuthorEdit(author, BbSurface.GamePost);
        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().Contain("data-bb-tag=\"private\"");
        html.Should().Contain("data-bb-addressees=\"B\"");
        html.Should().Contain("secret");
    }

    /// <summary>
    /// A tag the author forgot to close does not carry the block past the filter.
    /// </summary>
    /// <remarks>
    /// The [img] content pattern used to run to the next [/img] anywhere in the
    /// post, so an opening tag left unclosed swallowed everything down to the
    /// closing tag of the NEXT image — the private block included. What is
    /// swallowed is filed as the image's URL before the parse, which means it
    /// never becomes a node and the filter here is never asked about it: there
    /// was nothing to strip, and the block came back out of the tree walk
    /// verbatim.
    ///
    /// Both audiences, because they leaked through different doors. The HTML one
    /// dropped the image on the whitespace the swallowed text carried and took
    /// the author's own paragraph down with it; the plain-text one is what the
    /// stored search text is built from and what X-Dm-Audience: plain_text
    /// returns to anyone who asks for it.
    /// </remarks>
    [Theory]
    [InlineData("до [img]https://example.com/a.png\n[private=\"B\"]secret[/private]\n" +
                "[img]https://example.com/b.png[/img] после")]
    [InlineData("до [img]https://example.com/a.png[private=\"B\"]secret[/private]" +
                "[img]https://example.com/b.png[/img] после")]
    [InlineData("до [link]https://example.com/a\n[private=\"B\"]secret[/private]\n" +
                "[link]https://example.com/b[/link] после")]
    [InlineData("до [img=100 alt=\"x\"]https://example.com/a.png\n[private=\"B\"]secret[/private]\n" +
                "[img=100 alt=\"y\"]https://example.com/b.png[/img] после")]
    public void StripPrivate_WhenAnUnclosedTagAboveItReachesForTheNextClosingTag(string input)
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

    // ════════════════════════════════════════════════════════════════
    // The empty id is not an identity.
    //
    // Every rule below matches the reader against an id the content
    // carries. An anonymous reader's id is Guid.Empty, and Guid.Empty is
    // also what a context field nobody filled in holds — so a projection
    // that produced the text of a post without the fields deciding who may
    // read it handed each of these rules two empty ids and a match. The
    // rated-posts feed did exactly that, and served the [private] blocks of
    // every open game to readers who were not signed in.
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void HidePrivate_FromAnAnonymousReader_WhenTheContextWasNeverFilled()
    {
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.GamePost,
            Viewer = Viewer(UserRole.Guest, Guid.Empty),
            // What an unfilled post projection produces: a post with no known
            // author, in a game whose master id is the default Guid.
            PostAuthorUserId = Guid.Empty,
            GameLeadUserIds = [Guid.Empty]
        };

        var html = RenderWithContext("до [private=\"B\"]secret[/private] после",
            BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
        html.Should().Contain("до").And.Contain("после");
    }

    [Fact]
    public void HidePrivate_FromASignedInReader_CarryingTheEmptyId()
    {
        // The other half of the same coincidence: a subject that says it is
        // authenticated but carries no id. Neither half may be the one thing
        // keeping the block closed.
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.GamePost,
            Viewer = Viewer(UserRole.RegularUser, Guid.Empty),
            PostAuthorUserId = Guid.Empty,
            GameLeadUserIds = [Guid.Empty]
        };

        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
    }

    [Fact]
    public void HidePrivate_FromAnAnonymousReader_AddressedByAnEmptyIdInTheSnapshot()
    {
        var addressees = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal)
        {
            ["B"] = new HashSet<Guid> { Guid.Empty }
        };
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.GamePost,
            Viewer = Viewer(UserRole.Guest, Guid.Empty),
            PostAuthorUserId = AuthorId,
            GameLeadUserIds = [MasterId],
            PrivateAddresseeOwnerUserIdsByAttribute = addressees
        };

        var html = RenderWithContext("[private=\"B\"]secret[/private]", BbSurface.GamePost, ctx);

        html.Should().NotContain("secret");
    }

    [Fact]
    public void DropTheEmptyIdOnTheWayIntoTheContext()
    {
        // Not a rule of the filter but of the context that feeds it: the empty
        // id cannot be in the lead list or stand as the author, whatever the
        // caller passes.
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.GamePost,
            PostAuthorUserId = Guid.Empty,
            GameLeadUserIds = [Guid.Empty, MasterId, MasterId]
        };

        ctx.PostAuthorUserId.Should().BeNull();
        ctx.GameLeadUserIds.Should().Equal(MasterId);
    }

    [Fact]
    public void ReadNoViewerId_ForAReaderWhoIsNobody()
    {
        new RenderContext { Viewer = null }.ViewerUserId.Should().BeNull();
        new RenderContext { Viewer = Viewer(UserRole.Guest, Guid.Empty) }
            .ViewerUserId.Should().BeNull();
        new RenderContext { Viewer = Viewer(UserRole.RegularUser, Guid.Empty) }
            .ViewerUserId.Should().BeNull();
        new RenderContext { Viewer = Viewer(UserRole.RegularUser, OtherUser) }
            .ViewerUserId.Should().Be(OtherUser);
    }

    // ════════════════════════════════════════════════════════════════
    // Non-privacy content always passes through unchanged.
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void NonPrivacyContent_PassesThroughUnchanged_ForAnyViewer()
    {
        var html = Render("[b]bold[/b] plain text", BbSurface.Comment, Viewer(UserRole.Guest));

        html.Should().Contain("<strong>bold</strong>");
        html.Should().Contain("plain text");
    }

    // ════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════

    private string Render(string input, BbSurface surface, IAuthorizationSubject viewer)
    {
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = viewer
        };
        return RenderWithContext(input, surface, ctx);
    }

    private string RenderText(string input, BbSurface surface, RenderAudience audience)
    {
        var ctx = new RenderContext
        {
            Audience = audience,
            Surface = surface,
            Viewer = null
        };
        var parser = _parserProvider.GetForSurface(surface);
        var wrapper = (BbParserWrapper)parser;
        return wrapper.RenderText(input, ctx);
    }

    private string RenderWithContext(string input, BbSurface surface, RenderContext ctx)
    {
        var parser = ctx.Audience == RenderAudience.AuthorEdit
            ? _parserProvider.GetForAuthorEdit(surface)
            : _parserProvider.GetForSurface(surface);
        var wrapper = (BbParserWrapper)parser;
        return wrapper.RenderHtml(input, ctx);
    }

    private RenderContext DisplayCtxForGamePost(IAuthorizationSubject viewer) => new()
    {
        Audience = RenderAudience.Display,
        Surface = BbSurface.GamePost,
        Viewer = viewer,
        PostAuthorUserId = AuthorId,
        GameId = GameId
    };

    private static IAuthorizationSubject Viewer(UserRole role, Guid? userId = null) =>
        new TestSubject
        {
            UserId = userId ?? Guid.Parse("99999999-9999-9999-9999-999999999999"),
            Role = role,
            IsAuthenticated = role != UserRole.Guest,
            AccessPolicy = AccessPolicy.NotSpecified
        };

}
