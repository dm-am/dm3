using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Forum.Tests.Authorization;

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
            new Board
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
                new Board())
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
            new Board
            {
                CreateTopicPolicy = BoardAccessPolicy.Administrator | BoardAccessPolicy.SeniorModerator
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void ForbidCreateTopicUnderTheOrdinaryBan()
    {
        policyConverter
            .Setup(c => c.Convert(UserRole.RegularUser))
            .Returns(BoardAccessPolicy.RegularUser);

        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.RegularUser).WithAccessPolicy(AccessPolicy.DemocraticBan).Please(),
            ForumIntention.CreateTopic,
            new Board { CreateTopicPolicy = BoardAccessPolicy.RegularUser });

        // The board policy admits the role; the ban is what refuses
        actual.Should().BeFalse();
    }

    [Fact]
    public void ForbidTopicAdministrationWhenUserNotAdministratorOrLocalModerator()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Moderator).Please(),
            ForumIntention.AdministrateTopics,
            new Board
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
            new Board
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
            new Board
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
        resolver.IsAllowed(AuthenticatedUser.Guest, intention, new Board()).Should().BeFalse();
    }
}
