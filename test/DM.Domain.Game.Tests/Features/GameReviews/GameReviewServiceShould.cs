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
using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.GameReviews;

public class GameReviewServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<CreateGameReview>> _createValidator;
    private readonly Mock<IValidator<UpdateGameReview>> _updateValidator;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IGameReviewRepository> _repository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IProbationConfiguration> _probationConfig;
    private readonly GameReviewService _service;
    private readonly Guid _currentUserId;

    public GameReviewServiceShould()
    {
        _createValidator = Mock<IValidator<CreateGameReview>>();
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateGameReview>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _updateValidator = Mock<IValidator<UpdateGameReview>>();
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateGameReview>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<IGameReviewRepository>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identity.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _probationConfig = Mock<IProbationConfiguration>();
        _probationConfig.Setup(c => c.NewbiePostThreshold).Returns(100);

        _service = new GameReviewService(
            _createValidator.Object,
            _updateValidator.Object,
            _intentionManager.Object,
            _repository.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            _probationConfig.Object);
    }

    #region Create Tests

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var gameId = Guid.NewGuid();
        SetupSuccessfulCreate(gameId);

        await _service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameReviewIntention.Create), Times.Once);
    }

    [Fact]
    public async Task ThrowForbiddenWhenNewbieTryingToCreateReview()
    {
        var gameId = Guid.NewGuid();
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(50); // Newbie

        var act = async () => await _service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowForbiddenWhenUserHasNoPostsInGame()
    {
        var gameId = Guid.NewGuid();
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.CanReviewGameAsync(_currentUserId, gameId)).ReturnsAsync(false);

        var act = async () => await _service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowConflictWhenReviewAlreadyExists()
    {
        var gameId = Guid.NewGuid();
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.CanReviewGameAsync(_currentUserId, gameId)).ReturnsAsync(true);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, gameId)).ReturnsAsync(true);

        var act = async () => await _service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateReviewSuccessfully()
    {
        var gameId = Guid.NewGuid();
        var expectedReview = new GameReview { Id = Guid.NewGuid(), GameId = gameId, Text = "Great game!" };
        SetupSuccessfulCreate(gameId, expectedReview);

        var result = await _service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        result.Should().Be(expectedReview);
        _repository.Verify(r => r.CreateAsync(It.IsAny<CreateGameReviewEntity>()), Times.Once);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task ThrowNotFoundWhenReviewDoesNotExist()
    {
        var reviewId = Guid.NewGuid();
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync((GameReview?)null);

        var act = async () => await _service.GetAsync(reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnReviewWhenExists()
    {
        var reviewId = Guid.NewGuid();
        var review = new GameReview { Id = reviewId, Text = "Great game!" };
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
        var review = new GameReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Original text",
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdateGameReviewEntity>())).ReturnsAsync(review);

        await _service.UpdateAsync(new UpdateGameReview { ReviewId = reviewId, Text = "Updated text" });

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameReviewIntention.Edit, review), Times.Once);
    }

    [Fact]
    public async Task ThrowForbiddenWhenEditWindowExpired()
    {
        var reviewId = Guid.NewGuid();
        var review = new GameReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Original text",
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2) // Past edit window
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);

        var act = async () => await _service.UpdateAsync(new UpdateGameReview { ReviewId = reviewId, Text = "Updated text" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReturnUnmodifiedReviewWhenTextIsEmpty()
    {
        var reviewId = Guid.NewGuid();
        var review = new GameReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Original text",
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);

        var result = await _service.UpdateAsync(new UpdateGameReview { ReviewId = reviewId, Text = "" });

        result.Should().Be(review);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<UpdateGameReviewEntity>()), Times.Never);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var reviewId = Guid.NewGuid();
        var review = new GameReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Some text"
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdateGameReviewEntity>())).ReturnsAsync(review);

        await _service.DeleteAsync(reviewId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameReviewIntention.Delete, review), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteReview()
    {
        var reviewId = Guid.NewGuid();
        var review = new GameReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Some text"
        };
        _repository.Setup(r => r.GetAsync(reviewId)).ReturnsAsync(review);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdateGameReviewEntity>())).ReturnsAsync(review);

        await _service.DeleteAsync(reviewId);

        _repository.Verify(r => r.UpdateAsync(It.Is<UpdateGameReviewEntity>(e => e.IsRemoved == true)), Times.Once);
    }

    #endregion

    #region CanEdit Tests

    [Fact]
    public void ReturnTrueWhenWithinEditWindow()
    {
        var review = new GameReview { CreatedUtc = DateTimeOffset.UtcNow.AddHours(-12) };

        var result = _service.CanEdit(review);

        result.Should().BeTrue();
    }

    [Fact]
    public void ReturnFalseWhenOutsideEditWindow()
    {
        var review = new GameReview { CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2) };

        var result = _service.CanEdit(review);

        result.Should().BeFalse();
    }

    #endregion

    #region CanReview Tests

    [Fact]
    public async Task DelegateCanReviewToRepository()
    {
        var gameId = Guid.NewGuid();
        _repository.Setup(r => r.CanReviewGameAsync(_currentUserId, gameId)).ReturnsAsync(true);

        var result = await _service.CanReviewAsync(_currentUserId, gameId);

        result.Should().BeTrue();
        _repository.Verify(r => r.CanReviewGameAsync(_currentUserId, gameId), Times.Once);
    }

    #endregion

    private void SetupSuccessfulCreate(Guid gameId, GameReview? expectedReview = null)
    {
        expectedReview ??= new GameReview { Id = Guid.NewGuid(), GameId = gameId, Text = "Great game!" };

        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.CanReviewGameAsync(_currentUserId, gameId)).ReturnsAsync(true);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, gameId)).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(It.IsAny<CreateGameReviewEntity>())).ReturnsAsync(expectedReview);
    }
}
