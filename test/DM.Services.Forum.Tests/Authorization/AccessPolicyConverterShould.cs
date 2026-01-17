using DM.Services.Core.Dto.Enums;
using DM.Services.Forum.BusinessProcesses.Common;
using FluentAssertions;
using Xunit;

namespace DM.Services.Forum.Tests.Authorization;

public class AccessPolicyConverterShould
{
    private readonly AccessPolicyConverter converter = new();

    [Fact]
    public void ReturnGuestPolicyForGuestUser()
    {
        converter.Convert(UserRole.Guest).Should().Be(ForumAccessPolicy.Guest);
    }

    [Theory]
    [InlineData(UserRole.RegularUser, ForumAccessPolicy.Player)]
    [InlineData(UserRole.Admin, ForumAccessPolicy.Administrator)]
    [InlineData(UserRole.SeniorModerator, ForumAccessPolicy.SeniorModerator)]
    [InlineData(UserRole.Moderator, ForumAccessPolicy.RegularModerator)]
    [InlineData(UserRole.Mentor, ForumAccessPolicy.MentorModerator)]
    public void MapRolesAndPoliciesAccordingly(UserRole role, ForumAccessPolicy policy)
    {
        converter.Convert(role).Should().HaveFlag(policy);
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void ContainGuestAccessForNonGuestUsers(UserRole role)
    {
        converter.Convert(role).Should().HaveFlag(ForumAccessPolicy.Guest);
    }

    [Fact]
    public void AdminShouldHaveAllPolicies()
    {
        var result = converter.Convert(UserRole.Admin);
        result.Should().HaveFlag(ForumAccessPolicy.Guest);
        result.Should().HaveFlag(ForumAccessPolicy.Player);
        result.Should().HaveFlag(ForumAccessPolicy.MentorModerator);
        result.Should().HaveFlag(ForumAccessPolicy.RegularModerator);
        result.Should().HaveFlag(ForumAccessPolicy.SeniorModerator);
        result.Should().HaveFlag(ForumAccessPolicy.Administrator);
    }

    [Fact]
    public void MentorShouldNotHaveModeratorPolicies()
    {
        var result = converter.Convert(UserRole.Mentor);
        result.Should().HaveFlag(ForumAccessPolicy.MentorModerator);
        result.Should().NotHaveFlag(ForumAccessPolicy.RegularModerator);
        result.Should().NotHaveFlag(ForumAccessPolicy.SeniorModerator);
        result.Should().NotHaveFlag(ForumAccessPolicy.Administrator);
    }
}
