using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Dto;
using DM.Infrastructure.Core.Authorization;
using DM.Testing;
using DM.Testing.Dsl;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Authorization;

/// <summary>
/// This resolver is cross-cutting: forum, blog, publication and game comments all
/// authorize through it, so an inverted check here is a data-exposure bug in four
/// modules at once.
/// </summary>
public class CommentIntentionResolverShould : UnitTestBase
{
    private readonly CommentIntentionResolver resolver = new();

    private static Comment CommentBy(Guid authorId) =>
        new() { Author = new GeneralUser { UserId = authorId } };

    [Theory]
    [InlineData(CommentIntention.Edit)]
    [InlineData(CommentIntention.Delete)]
    public void AllowAuthorToEditAndDeleteOwnComment(CommentIntention intention)
    {
        var authorId = Guid.NewGuid();
        var user = Create.User(authorId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, CommentBy(authorId)).Should().BeTrue();
    }

    [Theory]
    [InlineData(CommentIntention.Edit)]
    [InlineData(CommentIntention.Delete)]
    public void ForbidRegularUserOnSomeoneElsesComment(CommentIntention intention)
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, CommentBy(Guid.NewGuid())).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowModeratorAndAboveToDeleteSomeoneElsesComment(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        resolver.IsAllowed(user, CommentIntention.Delete, CommentBy(Guid.NewGuid()))
            .Should().BeTrue();
    }

    [Fact]
    public void ForbidMentorOnSomeoneElsesComment()
    {
        // Mentor sits below Moderator in the role order; the boundary is worth
        // pinning because the check is written as ">= Moderator".
        var user = Create.User().WithRole(UserRole.Mentor).Please();

        resolver.IsAllowed(user, CommentIntention.Delete, CommentBy(Guid.NewGuid()))
            .Should().BeFalse();
    }

    [Fact]
    public void AllowLikingSomeoneElsesComment()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, CommentIntention.Like, CommentBy(Guid.NewGuid()))
            .Should().BeTrue();
    }

    [Fact]
    public void ForbidLikingOwnCommentEvenForAdministrator()
    {
        var authorId = Guid.NewGuid();
        var user = Create.User(authorId).WithRole(UserRole.Admin).Please();

        resolver.IsAllowed(user, CommentIntention.Like, CommentBy(authorId))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(CommentIntention.Edit)]
    [InlineData(CommentIntention.Delete)]
    [InlineData(CommentIntention.Like)]
    public void ForbidEverythingForGuest(CommentIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention, CommentBy(Guid.NewGuid()))
            .Should().BeFalse();
    }

    [Fact]
    public void ForbidGuestOnACommentWithAGuestShapedAuthorId()
    {
        // A guest carries the default UserId. Without the IsAuthenticated guard the
        // ownership comparison would match any comment whose author id is also
        // default, so this pins that the guard, not the comparison, does the work.
        resolver.IsAllowed(AuthenticatedUser.Guest, CommentIntention.Edit, CommentBy(Guid.Empty))
            .Should().BeFalse();
    }
}
