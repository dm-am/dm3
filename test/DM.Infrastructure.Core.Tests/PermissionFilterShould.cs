using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbVisibilityRender;

namespace DM.Infrastructure.Core.Tests;

public class PermissionFilterShould
{
    // An anonymous reader's id is Guid.Empty, as is a context field nobody filled in: no match on two empties.

    [Fact]
    public void HidePrivateFromAnAnonymousReaderWhenTheContextWasNeverFilled()
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
    public void HidePrivateFromASignedInReaderCarryingTheEmptyId()
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
    public void HidePrivateFromAnAnonymousReaderAddressedByAnEmptyIdInTheSnapshot()
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
    public void ReadNoViewerIdForAReaderWhoIsNobody()
    {
        new RenderContext { Viewer = null }.ViewerUserId.Should().BeNull();
        new RenderContext { Viewer = Viewer(UserRole.Guest, Guid.Empty) }
            .ViewerUserId.Should().BeNull();
        new RenderContext { Viewer = Viewer(UserRole.RegularUser, Guid.Empty) }
            .ViewerUserId.Should().BeNull();
        new RenderContext { Viewer = Viewer(UserRole.RegularUser, OtherUser) }
            .ViewerUserId.Should().Be(OtherUser);
    }

    [Fact]
    public void NonPrivacyContentPassesThroughUnchangedForAnyViewer()
    {
        var html = Render("[b]bold[/b] plain text", BbSurface.Comment, Viewer(UserRole.Guest));

        html.Should().Contain("<strong>bold</strong>");
        html.Should().Contain("plain text");
    }
}
