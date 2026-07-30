using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Core.Storage;
using DM.Testing;
using DM.Testing.Dsl;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Storage;

/// <summary>
/// Gates the moderation pages that list uploads across all users, i.e. other
/// people's files. Both intentions are moderator-and-above; the boundary below
/// Moderator is what this pins.
/// </summary>
public class UploadIntentionResolverShould : UnitTestBase
{
    private readonly UploadIntentionResolver resolver = new();

    [Theory]
    [InlineData(UploadIntention.ListAll, UserRole.Moderator)]
    [InlineData(UploadIntention.ListAll, UserRole.SeniorModerator)]
    [InlineData(UploadIntention.ListAll, UserRole.Admin)]
    [InlineData(UploadIntention.ListUser, UserRole.Moderator)]
    [InlineData(UploadIntention.ListUser, UserRole.SeniorModerator)]
    [InlineData(UploadIntention.ListUser, UserRole.Admin)]
    public void AllowModeratorAndAbove(UploadIntention intention, UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        resolver.IsAllowed(user, intention).Should().BeTrue();
    }

    [Theory]
    [InlineData(UploadIntention.ListAll, UserRole.RegularUser)]
    [InlineData(UploadIntention.ListAll, UserRole.Mentor)]
    [InlineData(UploadIntention.ListUser, UserRole.RegularUser)]
    [InlineData(UploadIntention.ListUser, UserRole.Mentor)]
    public void ForbidBelowModerator(UploadIntention intention, UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        resolver.IsAllowed(user, intention).Should().BeFalse();
    }

    [Theory]
    [InlineData(UploadIntention.ListAll)]
    [InlineData(UploadIntention.ListUser)]
    public void ForbidGuest(UploadIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention).Should().BeFalse();
    }

    [Fact]
    public void ForbidAnIntentionItDoesNotKnow()
    {
        // The resolver is a switch with a `_ => false` arm. A new UploadIntention
        // value must default to refused, not to allowed — this pins that the
        // fallthrough stays closed if someone reorders the arms.
        var admin = Create.User().WithRole(UserRole.Admin).Please();

        resolver.IsAllowed(admin, (UploadIntention)int.MaxValue).Should().BeFalse();
    }
}
