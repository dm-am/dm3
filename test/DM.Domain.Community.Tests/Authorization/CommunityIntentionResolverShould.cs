using DM.Domain.Community.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Community.Tests.Authorization;

/// <summary>
/// Gates the list of registrations that have not been activated yet. Those rows
/// are half-finished accounts — addresses that were typed in and never confirmed
/// — so this is account-level data about people who never joined the site.
/// </summary>
public class CommunityIntentionResolverShould
{
    private readonly CommunityIntentionResolver resolver = new();

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void ShowPendingRegistrationsToSeniorModerationAndAbove(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        resolver.IsAllowed(user, CommunityIntention.ViewPendingUsers).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void HidePendingRegistrationsFromEveryoneBelowSeniorModeration(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        // Junior moderation handles what people post, not who half-registered.
        resolver.IsAllowed(user, CommunityIntention.ViewPendingUsers).Should().BeFalse();
    }

    [Fact]
    public void HidePendingRegistrationsFromAGuest()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, CommunityIntention.ViewPendingUsers)
            .Should().BeFalse();
    }
}
