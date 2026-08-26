using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Game.Features.PostReviews;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

/// <summary>
/// Gates reviews of a game and reviews of a post. The two resolvers are separate
/// types with identical rules, so every case here runs against both: the day one
/// of them is edited alone, the half of the table belonging to the other fails
/// and the divergence has to be argued for instead of arriving silently.
/// </summary>
public class ReviewIntentionResolversShould : UnitTestBase
{
    /// <summary>
    /// Which of the two resolvers a case is being run against.
    /// </summary>
    public enum ReviewKind
    {
        Game,
        Post
    }

    private readonly GameReviewIntentionResolver gameReviews = new();
    private readonly PostReviewIntentionResolver postReviews = new();

    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static GameReview GameReviewBy(Guid authorId) =>
        new() { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), Author = new GeneralUser { UserId = authorId } };

    private static PostReview PostReviewBy(Guid authorId) =>
        new() { Id = Guid.NewGuid(), PostId = Guid.NewGuid(), Author = new GeneralUser { UserId = authorId } };

    private bool MayCreate(ReviewKind kind, IAuthorizationSubject user) => kind switch
    {
        ReviewKind.Game => gameReviews.IsAllowed(user, GameReviewIntention.Create),
        _ => postReviews.IsAllowed(user, PostReviewIntention.Create)
    };

    private bool MayEdit(ReviewKind kind, IAuthorizationSubject user, Guid authorId) => kind switch
    {
        ReviewKind.Game => gameReviews.IsAllowed(user, GameReviewIntention.Edit, GameReviewBy(authorId)),
        _ => postReviews.IsAllowed(user, PostReviewIntention.Edit, PostReviewBy(authorId))
    };

    private bool MayDelete(ReviewKind kind, IAuthorizationSubject user, Guid authorId) => kind switch
    {
        ReviewKind.Game => gameReviews.IsAllowed(user, GameReviewIntention.Delete, GameReviewBy(authorId)),
        _ => postReviews.IsAllowed(user, PostReviewIntention.Delete, PostReviewBy(authorId))
    };

    private bool MayCreateWithATarget(ReviewKind kind, IAuthorizationSubject user, Guid authorId) => kind switch
    {
        ReviewKind.Game => gameReviews.IsAllowed(user, GameReviewIntention.Create, GameReviewBy(authorId)),
        _ => postReviews.IsAllowed(user, PostReviewIntention.Create, PostReviewBy(authorId))
    };

    [Theory]
    [InlineData(ReviewKind.Game, UserRole.RegularUser)]
    [InlineData(ReviewKind.Game, UserRole.Admin)]
    [InlineData(ReviewKind.Post, UserRole.RegularUser)]
    [InlineData(ReviewKind.Post, UserRole.Admin)]
    public void LetAnyAuthenticatedUserWriteAReview(ReviewKind kind, UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        // Whether this user has standing to review this particular game or post
        // needs the database and stays in the service. The resolver refuses
        // anonymity and nothing else.
        MayCreate(kind, user).Should().BeTrue();
    }

    [Theory]
    [InlineData(ReviewKind.Game)]
    [InlineData(ReviewKind.Post)]
    public void NotLetAGuestWriteAReview(ReviewKind kind)
    {
        MayCreate(kind, AuthenticatedUser.Guest).Should().BeFalse();
    }

    [Theory]
    [InlineData(ReviewKind.Game)]
    [InlineData(ReviewKind.Post)]
    public void LetTheAuthorEditTheirOwnReview(ReviewKind kind)
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        MayEdit(kind, user, AuthorId).Should().BeTrue();
    }

    [Theory]
    [InlineData(ReviewKind.Game, UserRole.RegularUser)]
    [InlineData(ReviewKind.Game, UserRole.Moderator)]
    [InlineData(ReviewKind.Game, UserRole.Admin)]
    [InlineData(ReviewKind.Post, UserRole.RegularUser)]
    [InlineData(ReviewKind.Post, UserRole.Moderator)]
    [InlineData(ReviewKind.Post, UserRole.Admin)]
    public void NotLetAnybodyElseRewriteAReview(ReviewKind kind, UserRole role)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();

        // Moderation included: a review is signed, so the way to deal with a bad
        // one is to take it down, not to put different words under its name.
        MayEdit(kind, user, AuthorId).Should().BeFalse();
    }

    [Theory]
    [InlineData(ReviewKind.Game)]
    [InlineData(ReviewKind.Post)]
    public void LetTheAuthorDeleteTheirOwnReview(ReviewKind kind)
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        MayDelete(kind, user, AuthorId).Should().BeTrue();
    }

    [Theory]
    [InlineData(ReviewKind.Game, UserRole.SeniorModerator)]
    [InlineData(ReviewKind.Game, UserRole.Admin)]
    [InlineData(ReviewKind.Post, UserRole.SeniorModerator)]
    [InlineData(ReviewKind.Post, UserRole.Admin)]
    public void LetSeniorModerationDeleteAnyReview(ReviewKind kind, UserRole role)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();

        MayDelete(kind, user, AuthorId).Should().BeTrue();
    }

    [Theory]
    [InlineData(ReviewKind.Game, UserRole.RegularUser)]
    [InlineData(ReviewKind.Game, UserRole.Mentor)]
    [InlineData(ReviewKind.Game, UserRole.Moderator)]
    [InlineData(ReviewKind.Post, UserRole.RegularUser)]
    [InlineData(ReviewKind.Post, UserRole.Mentor)]
    [InlineData(ReviewKind.Post, UserRole.Moderator)]
    public void NotLetAnyoneBelowSeniorModerationDeleteSomebodyElsesReview(ReviewKind kind, UserRole role)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();

        // A mentor curates games. That is not a moderation rank and buys nothing
        // over other people's reviews.
        //
        // A moderator stops at the rank AUTHORIZATION.md draws: forum topics and
        // comments are theirs, a stated opinion about a game or a person is not.
        // Deleting one silences its author and moves a rating with it.
        MayDelete(kind, user, AuthorId).Should().BeFalse();
    }

    [Theory]
    [InlineData(ReviewKind.Game)]
    [InlineData(ReviewKind.Post)]
    public void NotAnswerCreateOnTheTargetedOverload(ReviewKind kind)
    {
        var user = Create.User(AuthorId).WithRole(UserRole.Admin).Please();

        // Create is a question about the user, so the overload that takes an
        // existing review must fall through rather than answer it.
        MayCreateWithATarget(kind, user, AuthorId).Should().BeFalse();
    }

    [Theory]
    [InlineData(ReviewKind.Game)]
    [InlineData(ReviewKind.Post)]
    public void NotAnswerEditOrDeleteWithoutAReview(ReviewKind kind)
    {
        var user = Create.User(AuthorId).WithRole(UserRole.Admin).Please();

        var edit = kind == ReviewKind.Game
            ? gameReviews.IsAllowed(user, GameReviewIntention.Edit)
            : postReviews.IsAllowed(user, PostReviewIntention.Edit);
        var delete = kind == ReviewKind.Game
            ? gameReviews.IsAllowed(user, GameReviewIntention.Delete)
            : postReviews.IsAllowed(user, PostReviewIntention.Delete);

        edit.Should().BeFalse();
        delete.Should().BeFalse();
    }
}
