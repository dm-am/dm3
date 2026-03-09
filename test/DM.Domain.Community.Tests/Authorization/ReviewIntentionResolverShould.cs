using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Community.Authorization;
using DM.Domain.Community.Tests.Dsl;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Reviews;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Community.Tests.Authorization;

public class ReviewIntentionResolverShould
{
    private readonly ReviewIntentionResolver _resolver = new();

    #region Simple intention (without target)

    [Theory]
    [InlineData(ReviewIntention.Create)]
    [InlineData(ReviewIntention.ReadUnapproved)]
    [InlineData(ReviewIntention.CreateUserReview)]
    [InlineData(ReviewIntention.CreateGameReview)]
    [InlineData(ReviewIntention.CreatePostReview)]
    public void ForbidAllForGuest(ReviewIntention intention)
    {
        _resolver.IsAllowed(AuthenticatedUser.Guest, intention).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void ForbidCreatePlatformReviewForNonSeniorModerator(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        _resolver.IsAllowed(user, ReviewIntention.Create).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowCreatePlatformReviewForSeniorModeratorOrHigher(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        _resolver.IsAllowed(user, ReviewIntention.Create).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowReadUnapprovedForSeniorModeratorOrHigher(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        _resolver.IsAllowed(user, ReviewIntention.ReadUnapproved).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void AllowCreateUserReviewForAnyAuthenticatedUser(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        _resolver.IsAllowed(user, ReviewIntention.CreateUserReview).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void AllowCreateGameReviewForAnyAuthenticatedUser(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        _resolver.IsAllowed(user, ReviewIntention.CreateGameReview).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    public void AllowCreatePostReviewForAnyAuthenticatedUser(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        _resolver.IsAllowed(user, ReviewIntention.CreatePostReview).Should().BeTrue();
    }

    #endregion

    #region With target Review

    [Fact]
    public void AllowEditUnapprovedPlatformReviewByAuthor()
    {
        var userId = Guid.NewGuid();
        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();
        var review = CreateReview(userId, approved: false);

        _resolver.IsAllowed(user, ReviewIntention.Edit, review).Should().BeTrue();
    }

    [Fact]
    public void ForbidEditApprovedPlatformReview()
    {
        var userId = Guid.NewGuid();
        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();
        var review = CreateReview(userId, approved: true);

        _resolver.IsAllowed(user, ReviewIntention.Edit, review).Should().BeFalse();
    }

    [Fact]
    public void ForbidEditPlatformReviewByNonAuthor()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        var review = CreateReview(Guid.NewGuid(), approved: false);

        _resolver.IsAllowed(user, ReviewIntention.Edit, review).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowApproveForSeniorModeratorOrHigher(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var review = CreateReview(Guid.NewGuid(), approved: false);

        _resolver.IsAllowed(user, ReviewIntention.Approve, review).Should().BeTrue();
    }

    [Fact]
    public void ForbidApproveAlreadyApprovedReview()
    {
        var user = Create.User().WithRole(UserRole.Admin).Please();
        var review = CreateReview(Guid.NewGuid(), approved: true);

        _resolver.IsAllowed(user, ReviewIntention.Approve, review).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void ForbidApproveForNonSeniorModerator(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var review = CreateReview(Guid.NewGuid(), approved: false);

        _resolver.IsAllowed(user, ReviewIntention.Approve, review).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowDeletePlatformReviewForSeniorModeratorOrHigher(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var review = CreateReview(Guid.NewGuid());

        _resolver.IsAllowed(user, ReviewIntention.Delete, review).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void ForbidDeletePlatformReviewForNonSeniorModerator(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var review = CreateReview(Guid.NewGuid());

        _resolver.IsAllowed(user, ReviewIntention.Delete, review).Should().BeFalse();
    }

    #endregion

    #region User Reviews

    [Fact]
    public void AllowEditUserReviewByAuthor()
    {
        var userId = Guid.NewGuid();
        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();
        var review = CreateReview(userId);

        _resolver.IsAllowed(user, ReviewIntention.EditUserReview, review).Should().BeTrue();
    }

    [Fact]
    public void ForbidEditUserReviewByNonAuthor()
    {
        var user = Create.User().WithRole(UserRole.Admin).Please();
        var review = CreateReview(Guid.NewGuid());

        _resolver.IsAllowed(user, ReviewIntention.EditUserReview, review).Should().BeFalse();
    }

    [Fact]
    public void AllowDeleteUserReviewByAuthor()
    {
        var userId = Guid.NewGuid();
        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();
        var review = CreateReview(userId);

        _resolver.IsAllowed(user, ReviewIntention.DeleteUserReview, review).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowDeleteUserReviewByModerator(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var review = CreateReview(Guid.NewGuid());

        _resolver.IsAllowed(user, ReviewIntention.DeleteUserReview, review).Should().BeTrue();
    }

    [Fact]
    public void ForbidDeleteUserReviewByNonAuthorNonModerator()
    {
        var user = Create.User().WithRole(UserRole.Mentor).Please();
        var review = CreateReview(Guid.NewGuid());

        _resolver.IsAllowed(user, ReviewIntention.DeleteUserReview, review).Should().BeFalse();
    }

    #endregion

    #region Game Reviews

    [Fact]
    public void AllowEditGameReviewByAuthor()
    {
        var userId = Guid.NewGuid();
        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();
        var review = CreateReview(userId);

        _resolver.IsAllowed(user, ReviewIntention.EditGameReview, review).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowDeleteGameReviewByModerator(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var review = CreateReview(Guid.NewGuid());

        _resolver.IsAllowed(user, ReviewIntention.DeleteGameReview, review).Should().BeTrue();
    }

    #endregion

    #region Post Reviews

    [Theory]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowDeletePostReviewByModerator(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var review = CreateReview(Guid.NewGuid());

        _resolver.IsAllowed(user, ReviewIntention.DeletePostReview, review).Should().BeTrue();
    }

    [Fact]
    public void AllowDeletePostReviewByAuthor()
    {
        var userId = Guid.NewGuid();
        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();
        var review = CreateReview(userId);

        _resolver.IsAllowed(user, ReviewIntention.DeletePostReview, review).Should().BeTrue();
    }

    #endregion

    private static Review CreateReview(Guid authorId, bool approved = false) => new()
    {
        Id = Guid.NewGuid(),
        Author = new GeneralUser { UserId = authorId },
        Approved = approved
    };
}
