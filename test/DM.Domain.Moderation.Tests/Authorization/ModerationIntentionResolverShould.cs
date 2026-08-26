using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Authorization;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
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

    /// <summary>
    /// The manual moderation watch sits at the rank that keeps the violators list
    /// and issues the warnings, not at the one that edits profile text.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Guest, false)]
    [InlineData(UserRole.RegularUser, false)]
    [InlineData(UserRole.Mentor, false)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.SeniorModerator, true)]
    [InlineData(UserRole.Admin, true)]
    public void AllowTheModerationWatchFromModeratorUpwards(UserRole role, bool expected)
    {
        var user = role == UserRole.Guest
            ? AuthenticatedUser.Guest
            : Create.User().WithRole(role).Please();

        resolver.IsAllowed(user, ModerationIntention.SetModerationWatch).Should().Be(expected);
    }

    /// <summary>
    /// Every intention the enum declares is granted to somebody, and refused to
    /// somebody. A member added with no arm behind it lands here.
    /// </summary>
    [Fact]
    public void AnswerEveryIntentionTheEnumDeclares()
    {
        var admin = Create.User().WithRole(UserRole.Admin).Please();

        foreach (var intention in System.Enum.GetValues<ModerationIntention>())
        {
            resolver.IsAllowed(admin, intention).Should().BeTrue(
                "an intention nobody can ever be granted is a rule with no subject");
            resolver.IsAllowed(AuthenticatedUser.Guest, intention).Should().BeFalse(
                "and one a guest is granted is not a moderation rule at all");
        }
    }
}
