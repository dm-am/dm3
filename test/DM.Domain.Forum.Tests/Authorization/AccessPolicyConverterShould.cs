using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Boards;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Forum.Tests.Authorization;

public class AccessPolicyConverterShould
{
    private readonly AccessPolicyConverter converter = new();

    [Fact]
    public void ReturnGuestPolicyForGuestUser()
    {
        converter.Convert(UserRole.Guest).Should().Be(BoardAccessPolicy.Guest);
    }

    [Theory]
    [InlineData(UserRole.RegularUser, BoardAccessPolicy.RegularUser)]
    [InlineData(UserRole.Admin, BoardAccessPolicy.Administrator)]
    [InlineData(UserRole.SeniorModerator, BoardAccessPolicy.SeniorModerator)]
    [InlineData(UserRole.Moderator, BoardAccessPolicy.Moderator)]
    [InlineData(UserRole.Mentor, BoardAccessPolicy.Mentor)]
    public void MapRolesAndPoliciesAccordingly(UserRole role, BoardAccessPolicy policy)
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
        converter.Convert(role).Should().HaveFlag(BoardAccessPolicy.Guest);
    }

    [Fact]
    public void AdminShouldHaveAllPolicies()
    {
        var result = converter.Convert(UserRole.Admin);
        result.Should().HaveFlag(BoardAccessPolicy.Guest);
        result.Should().HaveFlag(BoardAccessPolicy.RegularUser);
        result.Should().HaveFlag(BoardAccessPolicy.Mentor);
        result.Should().HaveFlag(BoardAccessPolicy.Moderator);
        result.Should().HaveFlag(BoardAccessPolicy.SeniorModerator);
        result.Should().HaveFlag(BoardAccessPolicy.Administrator);
    }

    [Fact]
    public void MentorShouldNotHaveModeratorPolicies()
    {
        var result = converter.Convert(UserRole.Mentor);
        result.Should().HaveFlag(BoardAccessPolicy.Mentor);
        result.Should().NotHaveFlag(BoardAccessPolicy.Moderator);
        result.Should().NotHaveFlag(BoardAccessPolicy.SeniorModerator);
        result.Should().NotHaveFlag(BoardAccessPolicy.Administrator);
    }
}
