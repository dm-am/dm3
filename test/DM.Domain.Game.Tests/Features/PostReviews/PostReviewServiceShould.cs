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
    private readonly Mock<IProbationConfiguration> _probationConfig;
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
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _probationConfig = Mock<IProbationConfiguration>();
        _probationConfig.Setup(c => c.NewbiePostThreshold).Returns(100);

        _blacklistRepository = Mock<IGameBlacklistRepository>();

        _service = new PostReviewService(
            _createValidator.Object,
            _updateValidator.Object,
            _intentionManager.Object,
            _repository.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            _probationConfig.Object,
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
        _repository.Setup(r => r.GetPostInfoAsync(postId)).ReturnsAsync((PostInfo?)null);
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

        _repository.Setup(r => r.GetPostInfoAsync(postId)).ReturnsAsync(postInfo);
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

        _repository.Setup(r => r.GetPostInfoAsync(postId)).ReturnsAsync(postInfo);
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

        _repository.Setup(r => r.GetPostInfoAsync(postId)).ReturnsAsync(postInfo);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(50); // Newbie
        _repository.Setup(r => r.ExistsAsync(_currentUserId, postId)).ReturnsAsync(false);
        _repository.Setup(r => r.HasRecentReviewInGameAsync(_currentUserId, gameId, It.IsAny<DateTimeOffset>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(It.IsAny<CreatePostReviewEntity>())).ReturnsAsync(expectedReview);

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

        _repository.Setup(r => r.GetPostInfoAsync(postId)).ReturnsAsync(postInfo);
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

        _repository.Setup(r => r.GetPostInfoAsync(postId)).ReturnsAsync(postInfo);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, postId)).ReturnsAsync(false);
        _repository.Setup(r => r.HasRecentReviewInGameAsync(_currentUserId, gameId, It.IsAny<DateTimeOffset>())).ReturnsAsync(true);

        var act = async () => await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task UpdateUserQualityRatingOnPositiveReview()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId);

        await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        _repository.Verify(r => r.UpdateUserQualityRatingAsync(postAuthorId, 1), Times.Once);
    }

    [Fact]
    public async Task UpdateUserQualityRatingOnNegativeReview()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId, ReviewSign.Negative);

        await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Negative });

        _repository.Verify(r => r.UpdateUserQualityRatingAsync(postAuthorId, -1), Times.Once);
    }

    [Fact]
    public async Task NotUpdateUserQualityRatingOnNeutralReview()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId, ReviewSign.Neutral);

        await _service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Neutral });

        _repository.Verify(r => r.UpdateUserQualityRatingAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task ThrowNotFoundWhenReviewDoesNotExist()
    {
        var reviewId = Guid.NewGuid();
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync((PostReview?)null);

        var act = async () => await _service.GetAsync(reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnReviewWhenExists()
    {
        var reviewId = Guid.NewGuid();
        var review = new PostReview { Id = reviewId };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);

        var result = await _service.GetAsync(reviewId);

        result.Should().Be(review);
    }

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
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>())).ReturnsAsync(review);

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
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2) // Past edit window
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);

        var act = async () => await _service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
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
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>())).ReturnsAsync(review);

        await _service.UpdateAsync(new UpdatePostReview { ReviewId = reviewId, Sign = ReviewSign.Negative });

        // When changing from Positive (+1) to Negative (-1):
        // 1. Revert old sign: -1 (undo +1)
        // 2. Apply new sign: -1 (apply -1)
        // Both calls pass -1, so we verify it's called exactly twice with -1
        _repository.Verify(r => r.UpdateUserQualityRatingAsync(postAuthorId, -1), Times.Exactly(2));
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
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>())).ReturnsAsync(review);

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
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdatePostReviewEntity>())).ReturnsAsync(review);

        await _service.DeleteAsync(reviewId);

        _repository.Verify(r => r.UpdateUserQualityRatingAsync(postAuthorId, -1), Times.Once);
    }

    #endregion

    #region CanEdit Tests

    [Fact]
    public void ReturnTrueWhenWithinEditWindow()
    {
        var review = new PostReview { CreatedUtc = DateTimeOffset.UtcNow.AddHours(-12) };

        var result = _service.CanEdit(review);

        result.Should().BeTrue();
    }

    [Fact]
    public void ReturnFalseWhenOutsideEditWindow()
    {
        var review = new PostReview { CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2) };

        var result = _service.CanEdit(review);

        result.Should().BeFalse();
    }

    #endregion

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

        _repository.Setup(r => r.GetPostInfoAsync(postId)).ReturnsAsync(postInfo);
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, postId)).ReturnsAsync(false);
        _repository.Setup(r => r.HasRecentReviewInGameAsync(_currentUserId, gameId, It.IsAny<DateTimeOffset>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(It.IsAny<CreatePostReviewEntity>())).ReturnsAsync(expectedReview);
    }
}
