using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

// Reading is not gated at all - the creation form fills itself from the catalogue - so the only
// question here is who may change it.
public class AttributeSchemaTemplateIntentionResolverShould
{
    private readonly AttributeSchemaTemplateIntentionResolver resolver = new();

    [Fact]
    public void RefuseAGuest() =>
        resolver.IsAllowed(AuthenticatedUser.Guest, AttributeSchemaTemplateIntention.Manage)
            .Should().BeFalse("a stranger reads the catalogue and writes nothing in it");

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void RefuseEverybodyBelowSeniorModeration(UserRole role) =>
        resolver.IsAllowed(Create.User().WithRole(role).Please(), AttributeSchemaTemplateIntention.Manage)
            .Should().BeFalse("the starting sheets of the site are the senior moderation's");

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowSeniorModerationAndAbove(UserRole role) =>
        resolver.IsAllowed(Create.User().WithRole(role).Please(), AttributeSchemaTemplateIntention.Manage)
            .Should().BeTrue();
}
