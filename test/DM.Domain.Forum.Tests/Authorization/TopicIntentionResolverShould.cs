using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Topics;
using DM.Domain.Forum.Tests.Dsl;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Forum.Tests.Authorization;

public class TopicIntentionResolverShould
{
    private readonly TopicIntentionResolver resolver = new();

    [Theory]
    [InlineData(TopicIntention.Like)]
    [InlineData(TopicIntention.CreateComment)]
    [InlineData(TopicIntention.Edit)]
    public void ForbidEverythingForGuest(TopicIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention, new Topic()).Should().BeFalse();
    }

    [Fact]
    public void ForbidCreateCommentInClosedTopic()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.RegularUser).Please(),
            TopicIntention.CreateComment,
            new Topic
            {
                IsClosed = true
            });
        actual.Should().BeFalse();
    }

    [Fact]
    public void AllowCreateCommentInOpenTopic()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.RegularUser).Please(),
            TopicIntention.CreateComment,
            new Topic
            {
                IsClosed = false
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void ForbidEditWhenUserNotAuthorAndNotLocalModeratorAndNotModerator()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Mentor).Please(),
            TopicIntention.Edit,
            new Topic
            {
                Author = Create.User().Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] {Guid.NewGuid(), Guid.NewGuid()}
                }
            });
        actual.Should().BeFalse();
    }

    [Fact]
    public void AllowEditWhenUserIsModerator()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Moderator).Please(),
            TopicIntention.Edit,
            new Topic
            {
                Author = Create.User().Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] {Guid.NewGuid(), Guid.NewGuid()}
                }
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void AllowEditWhenUserIsAuthor()
    {
        var userId = Guid.NewGuid();
        var actual = resolver.IsAllowed(
            Create.User(userId).WithRole(UserRole.Moderator).Please(),
            TopicIntention.Edit,
            new Topic
            {
                Author = Create.User(userId).Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] {Guid.NewGuid(), Guid.NewGuid()}
                }
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void AllowEditWhenUserIsLocalModerator()
    {
        var userId = Guid.NewGuid();
        var actual = resolver.IsAllowed(
            Create.User(userId).WithRole(UserRole.Moderator).Please(),
            TopicIntention.Edit,
            new Topic
            {
                Author = Create.User().Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] {userId, Guid.NewGuid()}
                }
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void AllowEditWhenUserIsAdministrator()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Admin).Please(),
            TopicIntention.Edit,
            new Topic
            {
                Author = Create.User().Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] {Guid.NewGuid(), Guid.NewGuid()}
                }
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void ForbidLikeWhenUserIsAuthor()
    {
        var userId = Guid.NewGuid();
        var actual = resolver.IsAllowed(
            Create.User(userId).WithRole(UserRole.Admin).Please(),
            TopicIntention.Like,
            new Topic
            {
                Author = Create.User(userId).Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] {Guid.NewGuid(), Guid.NewGuid()}
                }
            });
        actual.Should().BeFalse();
    }

    [Fact]
    public void ForbidLikeWhenUserIsNotAuthor()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Admin).Please(),
            TopicIntention.Like,
            new Topic
            {
                Author = Create.User().Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] {Guid.NewGuid(), Guid.NewGuid()}
                }
            });
        actual.Should().BeTrue();
    }
}