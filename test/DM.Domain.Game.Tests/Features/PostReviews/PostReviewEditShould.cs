using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
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

public class PostReviewEditShould : PostReviewServiceTestBase
{
    [Fact]
    public async Task AuthorizeEditAction()
    {
        var reviewId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = currentUserId },
            PostAuthor = new GeneralUser { UserId = Guid.NewGuid() },
            Sign = ReviewSign.Neutral,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        repository.GetAsync(reviewId).Returns(review);
        repository.UpdateAsync(Arg.Any<UpdatePostReviewEntity>()).Returns(review);

        await service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Positive });

        intentionManager.Received(1).ThrowIfForbidden(PostReviewIntention.Edit, review);
    }

    [Fact]
    public async Task ThrowForbiddenWhenEditWindowExpired()
    {
        var reviewId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = currentUserId },
            PostAuthor = new GeneralUser { UserId = Guid.NewGuid() },
            Sign = ReviewSign.Neutral,
            CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-16) // Past edit window
        };
        repository.GetAsync(reviewId).Returns(review);

        var act = async () => await service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SaveTheEditedTextWithTheSign()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId);
        repository.GetAsync(reviewId).Returns(review);
        UpdatePostReviewEntity? written = null;
        repository.UpdateAsync(Arg.Do<UpdatePostReviewEntity>(e => written = e))
            .Returns(review);

        await service.UpdateAsync(new UpdatePostReview
        {
            ReviewId = reviewId,
            Sign = ReviewSign.Negative,
            Text = "Перечитал и передумал"
        });

        written.Should().NotBeNull();
        written!.Sign.Should().Be(ReviewSign.Negative);
        written.Text.Should().Be("Перечитал и передумал");
        written.ModifiedByUserId.Should().Be(currentUserId);
    }

    // An omitted field keeps what is stored: correcting a typo must not reset the rating, and
    // re-picking the sign must not blank the sentence.
    [Fact]
    public async Task LeaveTheTextAloneWhenTheRequestDoesNotCarryIt()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId);
        repository.GetAsync(reviewId).Returns(review);
        UpdatePostReviewEntity? written = null;
        repository.UpdateAsync(Arg.Do<UpdatePostReviewEntity>(e => written = e))
            .Returns(review);

        await service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Positive });

        written.Should().NotBeNull();
        written!.Text.Should().BeNull();
    }

    /// <summary>
    /// The body renders on the Comment surface, where [mod] is a green
    /// moderator block. The create path unwraps it for an author below
    /// Moderator; an edit that did not would be the way around that rule.
    /// </summary>
    [Fact]
    public async Task UnwrapAModBlockAnOrdinaryAuthorPutInTheEditedText()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId);
        repository.GetAsync(reviewId).Returns(review);
        UpdatePostReviewEntity? written = null;
        repository.UpdateAsync(Arg.Do<UpdatePostReviewEntity>(e => written = e))
            .Returns(review);

        await service.UpdateAsync(new UpdatePostReview
        {
            ReviewId = reviewId,
            Text = "[mod]Предупреждение[/mod]"
        });

        written!.Text.Should().Be("Предупреждение");
    }

    // Probation holds on the way out as well: a newbie publishes the neutral review the form allows
    // them, and an edit is not a second door to a plus.
    [Fact]
    public async Task ThrowForbiddenWhenANewbieMovesTheirReviewOffNeutral()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId, ReviewSign.Neutral);
        repository.GetAsync(reviewId).Returns(review);
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold - 1);

        var act = async () => await service.UpdateAsync(
            new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<UpdatePostReviewEntity>());
    }

    [Fact]
    public async Task LetANewbieEditTheTextOfTheirNeutralReview()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId, ReviewSign.Neutral);
        repository.GetAsync(reviewId).Returns(review);
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold - 1);
        repository.UpdateAsync(Arg.Any<UpdatePostReviewEntity>()).Returns(review);

        await service.UpdateAsync(new UpdatePostReview
        {
            ReviewId = reviewId,
            Sign = ReviewSign.Neutral,
            Text = "Поправил опечатку"
        });

        await repository.Received(1).UpdateAsync(Arg.Any<UpdatePostReviewEntity>());
    }

    /// <summary>
    /// An omitted sign keeps the stored one, and it is what tells the write that
    /// the post author's counter does not move.
    /// </summary>
    /// <remarks>
    /// The mirror of the text rule above. The counter is moved by the write, from
    /// the row it locks — an edit that names no sign leaves the contribution of
    /// that row exactly where it was — so a service that filled the field in with
    /// the sign it had read would turn every typo correction into a counter move
    /// computed from a state somebody else may already have changed.
    /// </remarks>
    [Fact]
    public async Task LeaveTheSignAloneWhenTheRequestDoesNotCarryIt()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId);
        repository.GetAsync(reviewId).Returns(review);
        UpdatePostReviewEntity? written = null;
        repository.UpdateAsync(Arg.Do<UpdatePostReviewEntity>(e => written = e))
            .Returns(review);

        await service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Text = "Поправил опечатку" });

        written.Should().NotBeNull();
        written!.Sign.Should().BeNull();
    }

    [Fact]
    public void ReturnTrueWhenWithinEditWindow()
    {
        var review = new PostReview { CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5) };

        var result = service.CanEdit(review);

        result.Should().BeTrue();
    }

    [Fact]
    public void ReturnFalseWhenOutsideEditWindow()
    {
        // The window is the quarter of an hour a post and a comment give their author.
        var review = new PostReview { CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-16) };

        var result = service.CanEdit(review);

        result.Should().BeFalse();
    }

    // A review of the current user, published just now, so the edit window is open and the only
    // thing under test is what the update itself does.
    private PostReview EditableReview(Guid reviewId, ReviewSign sign = ReviewSign.Positive) => new()
    {
        Id = reviewId,
        Author = new GeneralUser { UserId = currentUserId },
        PostAuthor = new GeneralUser { UserId = Guid.NewGuid() },
        Sign = sign,
        CreatedUtc = DateTimeOffset.UtcNow
    };
}
