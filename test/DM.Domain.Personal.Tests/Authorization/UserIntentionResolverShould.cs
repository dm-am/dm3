using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Authorization;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Personal.Tests.Authorization;

public class UserIntentionResolverShould
{
    private readonly UserIntentionResolver _resolver = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    private AuthenticatedUser CreateUser(Guid userId, UserRole role = UserRole.RegularUser)
    {
        return new AuthenticatedUser
        {
            UserId = userId,
            Role = role
        };
    }

    private GeneralUser CreateTarget(Guid userId)
    {
        return new GeneralUser { UserId = userId };
    }

    [Fact]
    public void AllowUserToEditOwnProfile()
    {
        var user = CreateUser(_userId);
        var target = CreateTarget(_userId);

        var result = _resolver.IsAllowed(user, UserIntention.Edit, target);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyUserToEditOthersProfile()
    {
        var user = CreateUser(_userId);
        var target = CreateTarget(_otherUserId);

        var result = _resolver.IsAllowed(user, UserIntention.Edit, target);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowUserToWriteMessageToOthers()
    {
        var user = CreateUser(_userId);
        var target = CreateTarget(_otherUserId);

        var result = _resolver.IsAllowed(user, UserIntention.WriteMessage, target);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyUserToWriteMessageToSelf()
    {
        var user = CreateUser(_userId);
        var target = CreateTarget(_userId);

        var result = _resolver.IsAllowed(user, UserIntention.WriteMessage, target);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowSeniorModeratorToModerate()
    {
        var user = CreateUser(_userId, UserRole.SeniorModerator);
        var target = CreateTarget(_otherUserId);

        var result = _resolver.IsAllowed(user, UserIntention.Moderate, target);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyModeratorToModerate()
    {
        var user = CreateUser(_userId, UserRole.Moderator);
        var target = CreateTarget(_otherUserId);

        var result = _resolver.IsAllowed(user, UserIntention.Moderate, target);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowModeratorToReadModNotes()
    {
        var user = CreateUser(_userId, UserRole.Moderator);
        var target = CreateTarget(_otherUserId);

        var result = _resolver.IsAllowed(user, UserIntention.ReadModNotes, target);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyRegularUserToReadModNotes()
    {
        var user = CreateUser(_userId, UserRole.RegularUser);
        var target = CreateTarget(_otherUserId);

        var result = _resolver.IsAllowed(user, UserIntention.ReadModNotes, target);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowModeratorToCreateModNote()
    {
        var user = CreateUser(_userId, UserRole.Moderator);
        var target = CreateTarget(_otherUserId);

        var result = _resolver.IsAllowed(user, UserIntention.CreateModNote, target);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowModeratorToViewModeratedProfile()
    {
        var user = CreateUser(_userId, UserRole.Moderator);
        var target = CreateTarget(_otherUserId);

        var result = _resolver.IsAllowed(user, UserIntention.ViewModeratedProfile, target);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAdminToViewUserIpData()
    {
        var user = CreateUser(_userId, UserRole.Admin);
        var target = CreateTarget(_otherUserId);

        var result = _resolver.IsAllowed(user, UserIntention.ViewUserIpData, target);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenySeniorModeratorToViewUserIpData()
    {
        var user = CreateUser(_userId, UserRole.SeniorModerator);
        var target = CreateTarget(_otherUserId);

        var result = _resolver.IsAllowed(user, UserIntention.ViewUserIpData, target);

        result.Should().BeFalse();
    }
}

public class UserIntentionResolverWithoutTargetShould
{
    private readonly UserIntentionResolverWithoutTarget _resolver = new();

    private AuthenticatedUser CreateUser(UserRole role)
    {
        return new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Role = role
        };
    }

    [Fact]
    public void AllowSeniorModeratorToViewPendingUsers()
    {
        var user = CreateUser(UserRole.SeniorModerator);

        var result = _resolver.IsAllowed(user, UserIntention.ViewPendingUsers);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyModeratorToViewPendingUsers()
    {
        var user = CreateUser(UserRole.Moderator);

        var result = _resolver.IsAllowed(user, UserIntention.ViewPendingUsers);

        result.Should().BeFalse();
    }
}
