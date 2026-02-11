using System;
using DM.Services.Authentication.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Forum.Authorization;
using DM.Services.Forum.BusinessProcesses.Common;
using DM.Services.Forum.Tests.Dsl;
using DM.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Services.Forum.Tests.Authorization;

public class ForumIntentionResolverShould : UnitTestBase
{
    private readonly Mock<IAccessPolicyConverter> policyConverter;
    private readonly BoardIntentionResolver resolver;

    public ForumIntentionResolverShould()
    {
        policyConverter = Mock<IAccessPolicyConverter>();
        resolver = new BoardIntentionResolver(policyConverter.Object);
    }

    [Fact]
    public void ForbidCreateTopicWhenCreatePolicyMatchesNotUserRole()
    {
        policyConverter
            .Setup(c => c.Convert(UserRole.Admin))
            .Returns(
                BoardAccessPolicy.Moderator |
                BoardAccessPolicy.RegularUser |
                BoardAccessPolicy.Mentor);

        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Admin).Please(),
            ForumIntention.CreateTopic,
            new Dto.Output.Board
            {
                CreateTopicPolicy = BoardAccessPolicy.Guest | BoardAccessPolicy.SeniorModerator
            });
        actual.Should().BeFalse();
    }

    [Fact]
    public void ForbidCreateTopicWhenUserNotAuthenticated()
    {
        resolver.IsAllowed(
                Create.User().Please(),
                ForumIntention.CreateTopic,
                new Dto.Output.Board())
            .Should().BeFalse();
    }

    [Fact]
    public void AllowCreateTopicWhenCreatePolicyMatchUserRole()
    {
        policyConverter
            .Setup(c => c.Convert(UserRole.Admin))
            .Returns(
                BoardAccessPolicy.Guest |
                BoardAccessPolicy.RegularUser |
                BoardAccessPolicy.Administrator);

        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Admin).Please(),
            ForumIntention.CreateTopic,
            new Dto.Output.Board
            {
                CreateTopicPolicy = BoardAccessPolicy.Administrator | BoardAccessPolicy.SeniorModerator
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void ForbidTopicAdministrationWhenUserNotAdministratorOrLocalModerator()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Moderator).Please(),
            ForumIntention.AdministrateTopics,
            new Dto.Output.Board
            {
                ModeratorIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
            });
        actual.Should().BeFalse();
    }

    [Fact]
    public void AllowTopicAdministrationWhenUserAdministrator()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Admin).Please(),
            ForumIntention.AdministrateTopics,
            new Dto.Output.Board
            {
                ModeratorIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void AllowTopicAdministrationWhenUserLocalModerator()
    {
        var userId = Guid.NewGuid();
        var actual = resolver.IsAllowed(
            Create.User(userId).WithRole(UserRole.SeniorModerator).Please(),
            ForumIntention.AdministrateTopics,
            new Dto.Output.Board
            {
                ModeratorIds = new[] { Guid.NewGuid(), Guid.NewGuid(), userId }
            });
        actual.Should().BeTrue();
    }

    [Theory]
    [InlineData(ForumIntention.CreateTopic)]
    [InlineData(ForumIntention.AdministrateTopics)]
    public void AllowNothingWhenUserGuest(ForumIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention, new Dto.Output.Board()).Should().BeFalse();
    }
}