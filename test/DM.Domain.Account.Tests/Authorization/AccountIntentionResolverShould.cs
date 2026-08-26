using DM.Domain.Account.Authorization;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using DM.Domain.Core.Enums;
using DM.Testing;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Account.Tests.Authorization;

public class AccountIntentionResolverShould : UnitTestBase
{
    private readonly AccountIntentionResolver resolver;

    public AccountIntentionResolverShould()
    {
        resolver = new AccountIntentionResolver();
    }

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
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.Admin)]
    public void AllowChangePasswordForAnyAuthenticatedRole(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        resolver.IsAllowed(user, AccountIntention.ChangePassword).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.Admin)]
    public void AllowViewSessionsForAnyAuthenticatedRole(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        resolver.IsAllowed(user, AccountIntention.ViewSessions).Should().BeTrue();
    }
}
