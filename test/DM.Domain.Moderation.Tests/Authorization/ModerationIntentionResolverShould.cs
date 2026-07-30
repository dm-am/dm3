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
    [InlineData(ModerationIntention.ViewAllBans)]
    [InlineData(ModerationIntention.CreateWarning)]
    [InlineData(ModerationIntention.RemoveWarning)]
    [InlineData(ModerationIntention.ViewModNotes)]
    [InlineData(ModerationIntention.CreateModNote)]
    [InlineData(ModerationIntention.EditModNote)]
    [InlineData(ModerationIntention.DeleteModNote)]
    [InlineData(ModerationIntention.ViewTickets)]
    [InlineData(ModerationIntention.ResolveTicket)]
    [InlineData(ModerationIntention.ViewLinkedProfiles)]
    public void AllowModeratorIntentionsForModerator(ModerationIntention intention)
    {
        var user = Create.User().WithRole(UserRole.Moderator).Please();
        resolver.IsAllowed(user, intention).Should().BeTrue();
    }

    [Theory]
    [InlineData(ModerationIntention.CreateBan)]
    [InlineData(ModerationIntention.LiftBan)]
    public void AllowBanIntentionsForSeniorModerator(ModerationIntention intention)
    {
        var user = Create.User().WithRole(UserRole.SeniorModerator).Please();
        resolver.IsAllowed(user, intention).Should().BeTrue();
    }

    [Theory]
    [InlineData(ModerationIntention.CreateBan)]
    [InlineData(ModerationIntention.LiftBan)]
    public void ForbidBanIntentionsForModerator(ModerationIntention intention)
    {
        var user = Create.User().WithRole(UserRole.Moderator).Please();
        resolver.IsAllowed(user, intention).Should().BeFalse();
    }

    [Theory]
    [InlineData(ModerationIntention.ViewAllBans)]
    [InlineData(ModerationIntention.CreateBan)]
    [InlineData(ModerationIntention.LiftBan)]
    [InlineData(ModerationIntention.CreateWarning)]
    [InlineData(ModerationIntention.RemoveWarning)]
    [InlineData(ModerationIntention.ViewModNotes)]
    [InlineData(ModerationIntention.CreateModNote)]
    [InlineData(ModerationIntention.EditModNote)]
    [InlineData(ModerationIntention.DeleteModNote)]
    [InlineData(ModerationIntention.ViewTickets)]
    [InlineData(ModerationIntention.ResolveTicket)]
    [InlineData(ModerationIntention.ViewLinkedProfiles)]
    public void ForbidModeratorIntentionsForPlayer(ModerationIntention intention)
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        resolver.IsAllowed(user, intention).Should().BeFalse();
    }

    [Fact]
    public void AllowCreateTicketForAuthenticatedUser()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        resolver.IsAllowed(user, ModerationIntention.CreateTicket).Should().BeTrue();
    }

    [Fact]
    public void ForbidCreateTicketForGuest()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, ModerationIntention.CreateTicket).Should().BeFalse();
    }

    [Fact]
    public void AllowManageCredentialsForSeniorModerator()
    {
        var user = Create.User().WithRole(UserRole.SeniorModerator).Please();
        resolver.IsAllowed(user, ModerationIntention.ManageCredentials).Should().BeTrue();
    }

    [Fact]
    public void ForbidManageCredentialsForModerator()
    {
        var user = Create.User().WithRole(UserRole.Moderator).Please();
        resolver.IsAllowed(user, ModerationIntention.ManageCredentials).Should().BeFalse();
    }

    [Fact]
    public void AllowManageCredentialsForAdmin()
    {
        var user = Create.User().WithRole(UserRole.Admin).Please();
        resolver.IsAllowed(user, ModerationIntention.ManageCredentials).Should().BeTrue();
    }

    [Theory]
    [InlineData(ModerationIntention.ViewAllBans)]
    [InlineData(ModerationIntention.CreateBan)]
    [InlineData(ModerationIntention.LiftBan)]
    [InlineData(ModerationIntention.ViewTickets)]
    [InlineData(ModerationIntention.ResolveTicket)]
    public void ForbidAllModeratorIntentionsForGuest(ModerationIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention).Should().BeFalse();
    }
}
