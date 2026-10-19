using DM.Domain.Account.Authorization;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Account.Tests.Authorization;

public class AccountIntentionResolverShould
{
    private readonly AccountIntentionResolver resolver = new();

    [Theory]
    [InlineData(AccountIntention.ChangePassword)]
    [InlineData(AccountIntention.ChangeEmail)]
    [InlineData(AccountIntention.Deactivate)]
    [InlineData(AccountIntention.ViewSessions)]
    [InlineData(AccountIntention.TerminateSession)]
    public void AllowAllIntentionsWhenUserIsAuthenticated(AccountIntention intention)
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        resolver.IsAllowed(user, intention).Should().BeTrue();
    }

    [Theory]
    [InlineData(AccountIntention.ChangePassword)]
    [InlineData(AccountIntention.ChangeEmail)]
    [InlineData(AccountIntention.Deactivate)]
    [InlineData(AccountIntention.ViewSessions)]
    [InlineData(AccountIntention.TerminateSession)]
    public void ForbidAllIntentionsWhenUserIsGuest(AccountIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowUsernameChangeRollbackForSeniorModeration(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        resolver.IsAllowed(user, AccountIntention.RollbackUsernameChange).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void ForbidUsernameChangeRollbackBelowSeniorModeration(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        resolver.IsAllowed(user, AccountIntention.RollbackUsernameChange).Should().BeFalse();
    }
}
