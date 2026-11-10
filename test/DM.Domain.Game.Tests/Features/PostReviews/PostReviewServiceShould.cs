using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.PostReviews;
using DM.Domain.Game.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostReviews;

public class PostReviewServiceShould : PostReviewServiceTestBase
{
    [Fact]
    public async Task ThrowNotFoundWhenReviewDoesNotExist()
    {
        var postId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        ReachablePost(postId);
        repository.GetAsync(reviewId).Returns((PostReview?)null);

        var act = async () => await service.GetAsync(postId, reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnReviewWhenExists()
    {
        var postId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var review = new PostReview { Id = reviewId, PostId = postId };
        ReachablePost(postId);
        repository.GetAsync(reviewId).Returns(review);

        var result = await service.GetAsync(postId, reviewId);

        result.Should().Be(review);
    }

    /// <summary>
    /// A review is only as readable as the post it is about.
    /// </summary>
    [Fact]
    public async Task RefuseAReviewOfAPostTheReaderCannotOpen()
    {
        var postId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        repository.GetPostInfoAsync(postId, currentUserId).Returns((PostInfo?)null);

        var act = async () => await service.GetAsync(postId, reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await repository.DidNotReceive().GetAsync(reviewId);
    }

    // The route names a post and a review, and the two have to agree.
    [Fact]
    public async Task RefuseAReviewThatBelongsToAnotherPost()
    {
        var postId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        ReachablePost(postId);
        repository.GetAsync(reviewId).Returns(new PostReview { Id = reviewId, PostId = Guid.NewGuid() });

        var act = async () => await service.GetAsync(postId, reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    private void ReachablePost(Guid postId) =>
        repository.GetPostInfoAsync(postId, currentUserId)
            .Returns(new PostInfo { AuthorId = Guid.NewGuid(), GameId = Guid.NewGuid() });

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var reviewId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = currentUserId },
            PostAuthor = new GeneralUser { UserId = postAuthorId },
            Sign = ReviewSign.Neutral
        };
        repository.GetAsync(reviewId).Returns(review);
        repository.UpdateAsync(Arg.Any<UpdatePostReviewEntity>()).Returns(review);

        await service.DeleteAsync(reviewId);

        intentionManager.Received(1).ThrowIfForbidden(PostReviewIntention.Delete, review);
    }

    // A senior moderator may remove somebody else's review, so the removal has to say whose hand it
    // was and when.
    [Fact]
    public async Task RecordWhoRemovedTheReviewAndWhen()
    {
        var reviewId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            PostAuthor = new GeneralUser { UserId = Guid.NewGuid() },
            Sign = ReviewSign.Negative
        };
        repository.GetAsync(reviewId).Returns(review);
        UpdatePostReviewEntity? written = null;
        repository.UpdateAsync(Arg.Do<UpdatePostReviewEntity>(e => written = e))
            .Returns(review);

        await service.DeleteAsync(reviewId);

        written.Should().NotBeNull();
        written!.IsRemoved.Should().BeTrue();
        written.DeletedByUserId.Should().Be(currentUserId);
        written.DeletedUtc.Should().NotBeNull();
    }

    // No window on the author's own delete — the way out after the correction window has closed, as
    // with a post and a comment.
    [Fact]
    public async Task LetTheAuthorDeleteLongAfterTheEditWindowClosed()
    {
        var reviewId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = currentUserId },
            PostAuthor = new GeneralUser { UserId = postAuthorId },
            Sign = ReviewSign.Positive,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-30)
        };
        repository.GetAsync(reviewId).Returns(review);
        repository.UpdateAsync(Arg.Any<UpdatePostReviewEntity>()).Returns(review);

        await service.DeleteAsync(reviewId);

        await repository.Received(1).UpdateAsync(
            Arg.Is<UpdatePostReviewEntity>(e => e.IsRemoved == true));
    }
}
