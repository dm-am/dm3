using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Authorization;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Moderation.Tests.Authorization;

public class ModerationIntentionResolverShould : UnitTestBase
{
    private readonly ModerationIntentionResolver resolver;

    public ModerationIntentionResolverShould()
    {
        resolver = new ModerationIntentionResolver();
    }

    [Theory]
    [InlineData(ModerationIntention.ViewModNotes)]
    [InlineData(ModerationIntention.CreateModNote)]
    [InlineData(ModerationIntention.EditModNote)]
    [InlineData(ModerationIntention.DeleteModNote)]
    public void AllowModeratorIntentionsForModerator(ModerationIntention intention)
    {
        var user = Create.User().WithRole(UserRole.Moderator).Please();
        resolver.IsAllowed(user, intention).Should().BeTrue();
    }

    [Theory]
    [InlineData(ModerationIntention.ViewModNotes)]
    [InlineData(ModerationIntention.CreateModNote)]
    [InlineData(ModerationIntention.EditModNote)]
    [InlineData(ModerationIntention.DeleteModNote)]
    public void ForbidModeratorIntentionsForPlayer(ModerationIntention intention)
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        resolver.IsAllowed(user, intention).Should().BeFalse();
    }

    [Theory]
    [InlineData(ModerationIntention.ViewModNotes)]
    [InlineData(ModerationIntention.CreateModNote)]
    [InlineData(ModerationIntention.EditModNote)]
    [InlineData(ModerationIntention.DeleteModNote)]
    public void ForbidAllModeratorIntentionsForGuest(ModerationIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention).Should().BeFalse();
    }
}
