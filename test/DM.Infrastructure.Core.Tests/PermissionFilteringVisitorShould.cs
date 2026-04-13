using System;
using System.Collections.Generic;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Core.Parsing.Visitors;
using FluentAssertions;
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
    private static readonly Guid RoomId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid PostId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    // ════════════════════════════════════════════════════════════════
    // [mod] visibility — forum/comment/global chat surfaces only.
    // ════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(BbSurface.ForumTopic)]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.GlobalChatMessage)]
    public void StripMod_ForRegularUser_InAllowedSurfaces(BbSurface surface)
    {
        var html = Render("[mod]secret[/mod]",
            surface,
            Viewer(UserRole.RegularUser));

        html.Should().NotContain("secret");
        html.Should().NotContain("mod-block");
    }

    [Theory]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void RenderMod_ForModeratorPlus_InForum(UserRole role)
    {
        var html = Render("[mod]secret[/mod]", BbSurface.ForumTopic, Viewer(role));

        html.Should().Contain("mod-block");
        html.Should().Contain("secret");
    }

    [Fact]
    public void StripMod_InPlainTextAudience()
    {
        var text = RenderText("before [mod]secret[/mod] after",
            BbSurface.ForumTopic,
            audience: RenderAudience.PlainText);

        text.Should().NotContain("secret");
        text.Should().Contain("before");
        text.Should().Contain("after");
    }

    [Fact]
    public void RenderMod_WithRoundTripAttributes_InAuthorEdit()
    {
        var author = Viewer(UserRole.Moderator, AuthorId);
        var ctx = RenderContext.ForAuthorEdit(author, BbSurface.ForumTopic);
        var html = RenderWithContext("[mod]secret[/mod]", BbSurface.ForumTopic, ctx);

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

    // ════════════════════════════════════════════════════════════════
    // Non-privacy content always passes through unchanged.
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void NonPrivacyContent_PassesThroughUnchanged_ForAnyViewer()
    {
        var html = Render("[b]bold[/b] plain text", BbSurface.ForumTopic, Viewer(UserRole.Guest));

        html.Should().Contain("<strong>bold</strong>");
        html.Should().Contain("plain text");
    }

    [Fact]
    public void ContentWithoutPrivacyTags_IsNotDetectedAsPrivacySensitive()
    {
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.ForumTopic,
            Viewer = Viewer(UserRole.RegularUser)
        };
        var plan = PermissionFilteringVisitor.Prepare("[b]bold[/b]", ctx);
        plan.HasPrivacyTags.Should().BeFalse();
    }

    [Theory]
    [InlineData("[mod]x[/mod]")]
    [InlineData("[private=\"A\"]x[/private]")]
    [InlineData("[private]x[/private]")]
    [InlineData("before [mod]x[/mod] after")]
    public void ContentWithPrivacyTags_IsDetected(string input)
    {
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.ForumTopic,
            Viewer = Viewer(UserRole.RegularUser)
        };
        var plan = PermissionFilteringVisitor.Prepare(input, ctx);
        plan.HasPrivacyTags.Should().BeTrue();
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
        PostId = PostId,
        GameId = GameId,
        RoomId = RoomId
    };

    private static IAuthorizationSubject Viewer(UserRole role, Guid? userId = null) =>
        new TestSubject
        {
            UserId = userId ?? Guid.Parse("99999999-9999-9999-9999-999999999999"),
            Role = role,
            IsAuthenticated = role != UserRole.Guest,
            AccessPolicy = AccessPolicy.NotSpecified
        };

    private sealed class TestSubject : IAuthorizationSubject
    {
        public Guid UserId { get; init; }
        public UserRole Role { get; init; }
        public bool IsAuthenticated { get; init; }
        public AccessPolicy AccessPolicy { get; init; }
    }
}
