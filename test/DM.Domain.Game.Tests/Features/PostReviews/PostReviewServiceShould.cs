using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.PostReviews;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostReviews;

public class PostReviewServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<CreatePostReview>> _createValidator;
    private readonly Mock<IValidator<UpdatePostReview>> _updateValidator;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IPostReviewRepository> _repository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IGameBlacklistRepository> _blacklistRepository;
    private readonly PostReviewService _service;
    private readonly Guid _currentUserId;

    public PostReviewServiceShould()
    {
        _createValidator = Mock<IValidator<CreatePostReview>>();
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<CreatePostReview>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _updateValidator = Mock<IValidator<UpdatePostReview>>();
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdatePostReview>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<IPostReviewRepository>();

        _currentUserId = Guid.NewGuid();

        // Past probation by default. Zero is the newbie answer, and every case
        // about a newbie says so for itself; leaving it as the default made the
        // probation rule fire in cases that are not about it at all.
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);

        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _blacklistRepository = Mock<IGameBlacklistRepository>();

        _service = new PostReviewService(
            _createValidator.Object,
            _updateValidator.Object,
            _intentionManager.Object,
            _repository.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            _blacklistRepository.Object);
    }

    [Fact]
    public async Task ThrowForbiddenWhenBlacklistedFromTheGame()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId);
        _blacklistRepository
            .Setup(r => r.IsBlocked(gameId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await _service.CreateAsync(
            new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        // The game's posts are open to a blacklisted reader and stay open, so the
        // refusal has to sit on rating them rather than on seeing them
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    #region Create Tests

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId);

        await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        _intentionManager.Verify(m => m.ThrowIfForbidden(PostReviewIntention.Create), Times.Once);
    }

    [Fact]
    public async Task ThrowNotFoundWhenPostDoesNotExist()
    {
        var postId = Guid.NewGuid();
        _repository.Setup(r => r.GetPostInfoAsync(postId, _currentUserId)).ReturnsAsync((PostInfo?)null);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);

        var act = async () => await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ThrowForbiddenWhenReviewingOwnPost()
    {
        var postId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var postInfo = new PostInfo { AuthorId = _currentUserId, GameId = gameId };

        _repository.Setup(r => r.GetPostInfoAsync(postId, _currentUserId)).ReturnsAsync(postInfo);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);

        var act = async () => await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowForbiddenWhenNewbieTryingToCreateNonNeutralReview()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var postInfo = new PostInfo { AuthorId = postAuthorId, GameId = gameId };

        _repository.Setup(r => r.GetPostInfoAsync(postId, _currentUserId)).ReturnsAsync(postInfo);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(50); // Newbie

        var act = async () => await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AllowNewbieToCreateNeutralReview()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var postInfo = new PostInfo { AuthorId = postAuthorId, GameId = gameId };
        var expectedReview = new PostReview { Id = Guid.NewGuid(), PostId = postId };

        _repository.Setup(r => r.GetPostInfoAsync(postId, _currentUserId)).ReturnsAsync(postInfo);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(50); // Newbie
        _repository.Setup(r => r.ExistsAsync(_currentUserId, postId)).ReturnsAsync(false);
        _repository.Setup(r => r.HasRecentReviewInGameAsync(_currentUserId, gameId, It.IsAny<DateTimeOffset>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(It.IsAny<CreatePostReviewEntity>(), It.IsAny<int>())).ReturnsAsync(expectedReview);

        var result = await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Neutral });

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ThrowConflictWhenReviewAlreadyExists()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var postInfo = new PostInfo { AuthorId = postAuthorId, GameId = gameId };

        _repository.Setup(r => r.GetPostInfoAsync(postId, _currentUserId)).ReturnsAsync(postInfo);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, postId)).ReturnsAsync(true);

        var act = async () => await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ThrowTooManyRequestsWhenCooldownNotExpired()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var postInfo = new PostInfo { AuthorId = postAuthorId, GameId = gameId };

        _repository.Setup(r => r.GetPostInfoAsync(postId, _currentUserId)).ReturnsAsync(postInfo);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, postId)).ReturnsAsync(false);
        _repository.Setup(r => r.HasRecentReviewInGameAsync(_currentUserId, gameId, It.IsAny<DateTimeOffset>())).ReturnsAsync(true);

        var act = async () => await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// The post is looked up as the rater, not as nobody.
    /// </summary>
    /// <remarks>
    /// Room access is the only thing standing between a rating and a post in a
    /// private room, and it is applied where the post is read. Reading it without
    /// saying who is asking let anybody who learned an identifier move its
    /// author's quality rating from outside the game.
    /// </remarks>
    [Fact]
    public async Task ReadThePostAsTheRaterSoRoomAccessApplies()
    {
        var postId = Guid.NewGuid();
        SetupSuccessfulCreate(postId, Guid.NewGuid(), Guid.NewGuid());

        await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        _repository.Verify(r => r.GetPostInfoAsync(postId, _currentUserId), Times.Once);
    }

    /// <summary>
    /// The counter the post author is owed rides on the call that writes the
    /// review, and is not a write of its own.
    /// </summary>
    /// <remarks>
    /// QualityRating is stored and not summed: two writes meant two commits, and
    /// a refusal between them left the profile carrying a review that does not
    /// exist with nothing to recompute it from. The repository takes the delta as
    /// an argument now, so the pair cannot come apart above it — what it does with
    /// them is asserted against Postgres in PostReviewRepositoryShould.
    /// </remarks>
    [Fact]
    public async Task UpdateUserQualityRatingOnPositiveReview()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId);

        await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        _repository.Verify(r => r.CreateAsync(
            It.Is<CreatePostReviewEntity>(e => e.PostAuthorId == postAuthorId), 1), Times.Once);
    }

    [Fact]
    public async Task UpdateUserQualityRatingOnNegativeReview()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId, ReviewSign.Negative);

        await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Negative });

        _repository.Verify(r => r.CreateAsync(
            It.Is<CreatePostReviewEntity>(e => e.PostAuthorId == postAuthorId), -1), Times.Once);
    }

    [Fact]
    public async Task NotUpdateUserQualityRatingOnNeutralReview()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId, ReviewSign.Neutral);

        await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Neutral });

        _repository.Verify(r => r.CreateAsync(It.IsAny<CreatePostReviewEntity>(), 0), Times.Once);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task ThrowNotFoundWhenReviewDoesNotExist()
    {
        var postId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        ReachablePost(postId);
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync((PostReview?)null);

        var act = async () => await _service.GetAsync(postId, reviewId);

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
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);

        var result = await _service.GetAsync(postId, reviewId);

        result.Should().Be(review);
    }

    /// <summary>
    /// A review is only as readable as the post it is about.
    /// </summary>
    /// <remarks>
    /// The read used to go by review identifier alone, while the create path
    /// next to it scopes its post read by room access. So the body of a review
    /// on a post in a private room — and with it the post author and the game —
    /// came back to anybody holding the identifier.
    /// </remarks>
    [Fact]
    public async Task RefuseAReviewOfAPostTheReaderCannotOpen()
    {
        var postId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        _repository.Setup(r => r.GetPostInfoAsync(postId, _currentUserId)).ReturnsAsync((PostInfo?)null);

        var act = async () => await _service.GetAsync(postId, reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        _repository.Verify(r => r.GetAsync(reviewId), Times.Never);
    }

    /// <summary>
    /// The route names a post and a review, and the two have to agree.
    /// </summary>
    [Fact]
    public async Task RefuseAReviewThatBelongsToAnotherPost()
    {
        var postId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        ReachablePost(postId);
        _repository.Setup(r => r.GetAsync(reviewId))
            .ReturnsAsync(new PostReview { Id = reviewId, PostId = Guid.NewGuid() });

        var act = async () => await _service.GetAsync(postId, reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    private void ReachablePost(Guid postId) =>
        _repository.Setup(r => r.GetPostInfoAsync(postId, _currentUserId))
            .ReturnsAsync(new PostInfo { AuthorId = Guid.NewGuid(), GameId = Guid.NewGuid() });

    #endregion

    #region Update Tests

    [Fact]
    public async Task AuthorizeEditAction()
    {
        var reviewId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            PostAuthor = new GeneralUser { UserId = Guid.NewGuid() },
            Sign = ReviewSign.Neutral,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>())).ReturnsAsync(review);

        await _service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Positive });

        _intentionManager.Verify(m => m.ThrowIfForbidden(PostReviewIntention.Edit, review), Times.Once);
    }

    [Fact]
    public async Task ThrowForbiddenWhenEditWindowExpired()
    {
        var reviewId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            PostAuthor = new GeneralUser { UserId = Guid.NewGuid() },
            Sign = ReviewSign.Neutral,
            CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-16) // Past edit window
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);

        var act = async () => await _service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The whole point of the edit: a review is a sign and the sentence under
    /// it, and both reach storage.
    /// </summary>
    [Fact]
    public async Task SaveTheEditedTextWithTheSign()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId);
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        UpdatePostReviewEntity? written = null;
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>()))
            .Callback<UpdatePostReviewEntity, int>((e, _) => written = e)
            .ReturnsAsync(review);

        await _service.UpdateAsync(new UpdatePostReview
        {
            ReviewId = reviewId,
            Sign = ReviewSign.Negative,
            Text = "Перечитал и передумал"
        });

        written.Should().NotBeNull();
        written!.Sign.Should().Be(ReviewSign.Negative);
        written.Text.Should().Be("Перечитал и передумал");
        written.ModifiedByUserId.Should().Be(_currentUserId);
    }

    /// <summary>
    /// An omitted field keeps what is stored: correcting a typo must not reset
    /// the rating, and re-picking the sign must not blank the sentence.
    /// </summary>
    [Fact]
    public async Task LeaveTheTextAloneWhenTheRequestDoesNotCarryIt()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId);
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        UpdatePostReviewEntity? written = null;
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>()))
            .Callback<UpdatePostReviewEntity, int>((e, _) => written = e)
            .ReturnsAsync(review);

        await _service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Positive });

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
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        UpdatePostReviewEntity? written = null;
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>()))
            .Callback<UpdatePostReviewEntity, int>((e, _) => written = e)
            .ReturnsAsync(review);

        await _service.UpdateAsync(new UpdatePostReview
        {
            ReviewId = reviewId,
            Text = "[mod]Предупреждение[/mod]"
        });

        written!.Text.Should().Be("Предупреждение");
    }

    /// <summary>
    /// Probation holds on the way out as well: a newbie publishes the neutral
    /// review the form allows them, and an edit is not a second door to a plus.
    /// </summary>
    [Fact]
    public async Task ThrowForbiddenWhenANewbieMovesTheirReviewOffNeutral()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId, ReviewSign.Neutral);
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(10);

        var act = async () => await _service.UpdateAsync(
            new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>
    /// The same newbie may still fix the wording of what they wrote.
    /// </summary>
    [Fact]
    public async Task LetANewbieEditTheTextOfTheirNeutralReview()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId, ReviewSign.Neutral);
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(10);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>())).ReturnsAsync(review);

        await _service.UpdateAsync(new UpdatePostReview
        {
            ReviewId = reviewId,
            Sign = ReviewSign.Neutral,
            Text = "Поправил опечатку"
        });

        _repository.Verify(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task UpdateQualityRatingOnSignChange()
    {
        var reviewId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            PostAuthor = new GeneralUser { UserId = postAuthorId },
            Sign = ReviewSign.Positive,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>())).ReturnsAsync(review);

        await _service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Negative });

        // Positive (+1) to Negative (-1) is worth -2 to the post author: the old
        // sign taken back and the new one applied. One number, on the call that
        // writes the sign — the two deltas used to be two commits of their own,
        // both before the row they belong to was written.
        _repository.Verify(r => r.UpdateAsync(
            It.Is<UpdatePostReviewEntity>(e => e.Sign == ReviewSign.Negative), -2), Times.Once);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>()), Times.Once);
    }

    /// <summary>
    /// An edit that leaves the sign where it is owes the counter nothing.
    /// </summary>
    [Fact]
    public async Task MoveNoQualityRatingWhenOnlyTheTextChanges()
    {
        var reviewId = Guid.NewGuid();
        var review = EditableReview(reviewId);
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>()))
            .ReturnsAsync(review);

        await _service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Text = "Поправил опечатку" });

        _repository.Verify(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), 0), Times.Once);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var reviewId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            PostAuthor = new GeneralUser { UserId = postAuthorId },
            Sign = ReviewSign.Neutral
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>())).ReturnsAsync(review);

        await _service.DeleteAsync(reviewId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(PostReviewIntention.Delete, review), Times.Once);
    }

    [Fact]
    public async Task RevertQualityRatingOnDelete()
    {
        var reviewId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            PostAuthor = new GeneralUser { UserId = postAuthorId },
            Sign = ReviewSign.Positive
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>())).ReturnsAsync(review);

        await _service.DeleteAsync(reviewId);

        // On the removal itself, so the counter and the flag land together
        _repository.Verify(r => r.UpdateAsync(
            It.Is<UpdatePostReviewEntity>(e => e.IsRemoved == true), -1), Times.Once);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>()), Times.Once);
    }

    /// <summary>
    /// A neutral review took nothing from the post author, so removing it gives
    /// nothing back.
    /// </summary>
    [Fact]
    public async Task MoveNoQualityRatingWhenRemovingANeutralReview()
    {
        var reviewId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            PostAuthor = new GeneralUser { UserId = Guid.NewGuid() },
            Sign = ReviewSign.Neutral
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>()))
            .ReturnsAsync(review);

        await _service.DeleteAsync(reviewId);

        _repository.Verify(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), 0), Times.Once);
    }

    /// <summary>
    /// A senior moderator may remove somebody else's review, so the removal has
    /// to say whose hand it was and when. Written with the flag, in one call.
    /// </summary>
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
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        UpdatePostReviewEntity? written = null;
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>()))
            .Callback<UpdatePostReviewEntity, int>((e, _) => written = e)
            .ReturnsAsync(review);

        await _service.DeleteAsync(reviewId);

        written.Should().NotBeNull();
        written!.IsRemoved.Should().BeTrue();
        written.DeletedByUserId.Should().Be(_currentUserId);
        written.DeletedUtc.Should().NotBeNull();
    }

    /// <summary>
    /// No window on the author's own delete — the way out after the correction
    /// window has closed, as with a post and a comment.
    /// </summary>
    [Fact]
    public async Task LetTheAuthorDeleteLongAfterTheEditWindowClosed()
    {
        var reviewId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var review = new PostReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            PostAuthor = new GeneralUser { UserId = postAuthorId },
            Sign = ReviewSign.Positive,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-30)
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>(), It.IsAny<int>())).ReturnsAsync(review);

        await _service.DeleteAsync(reviewId);

        _repository.Verify(r => r.UpdateAsync(
            It.Is<UpdatePostReviewEntity>(e => e.IsRemoved == true), -1), Times.Once);
    }

    #endregion

    #region CanEdit Tests

    [Fact]
    public void ReturnTrueWhenWithinEditWindow()
    {
        var review = new PostReview { CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5) };

        var result = _service.CanEdit(review);

        result.Should().BeTrue();
    }

    [Fact]
    public void ReturnFalseWhenOutsideEditWindow()
    {
        // The window is the quarter of an hour a post and a comment give their
        // author, not the day the sibling review entities used to give this one.
        var review = new PostReview { CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-16) };

        var result = _service.CanEdit(review);

        result.Should().BeFalse();
    }

    #endregion

    /// <summary>
    /// A review of the current user, published just now, so the edit window is
    /// open and the only thing under test is what the update itself does.
    /// </summary>
    private PostReview EditableReview(Guid reviewId, ReviewSign sign = ReviewSign.Positive) => new()
    {
        Id = reviewId,
        Author = new GeneralUser { UserId = _currentUserId },
        PostAuthor = new GeneralUser { UserId = Guid.NewGuid() },
        Sign = sign,
        CreatedUtc = DateTimeOffset.UtcNow
    };

    private void SetupSuccessfulCreate(Guid postId, Guid postAuthorId, Guid gameId, ReviewSign sign = ReviewSign.Positive)
    {
        var postInfo = new PostInfo { AuthorId = postAuthorId, GameId = gameId };
        var expectedReview = new PostReview
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            Sign = sign,
            PostAuthor = new GeneralUser { UserId = postAuthorId }
        };

        _repository.Setup(r => r.GetPostInfoAsync(postId, _currentUserId)).ReturnsAsync(postInfo);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, postId)).ReturnsAsync(false);
        _repository.Setup(r => r.HasRecentReviewInGameAsync(_currentUserId, gameId, It.IsAny<DateTimeOffset>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(It.IsAny<CreatePostReviewEntity>(), It.IsAny<int>())).ReturnsAsync(expectedReview);
    }
}
