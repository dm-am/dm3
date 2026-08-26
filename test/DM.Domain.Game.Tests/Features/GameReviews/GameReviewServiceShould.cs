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
using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Game.Features.Games;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.GameReviews;

public class GameReviewServiceShould : UnitTestBase
{
    private readonly IValidator<CreateGameReview> _createValidator;
    private readonly IValidator<UpdateGameReview> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameReviewRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGameBlacklistRepository _blacklistRepository;
    private readonly GameReviewService _service;
    private readonly Guid _currentUserId;

    public GameReviewServiceShould()
    {
        _createValidator = Mock<IValidator<CreateGameReview>>();
        _createValidator.ValidateAsync(Arg.Any<CreateGameReview>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _updateValidator = Mock<IValidator<UpdateGameReview>>();
        _updateValidator.ValidateAsync(Arg.Any<UpdateGameReview>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<IGameReviewRepository>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Create().Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _blacklistRepository = Mock<IGameBlacklistRepository>();

        _service = new GameReviewService(
            _createValidator,
            _updateValidator,
            _intentionManager,
            _repository,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider,
            _blacklistRepository);
    }

    [Fact]
    public async Task ThrowForbiddenWhenBlacklistedFromTheGame()
    {
        var gameId = Guid.NewGuid();
        SetupSuccessfulCreate(gameId);
        _blacklistRepository
            .IsBlocked(gameId, _currentUserId, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        // Reading the game is open to a blacklisted user, so they reach the review
        // form, and the right to review survives being removed from the game: one
        // post in it is enough and it is not taken back. The blacklist closes
        // writing, and a review is writing.
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    #region Create Tests

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var gameId = Guid.NewGuid();
        SetupSuccessfulCreate(gameId);

        await _service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        _intentionManager.Received(1).ThrowIfForbidden(GameReviewIntention.Create);
    }

    [Fact]
    public async Task ThrowForbiddenWhenNewbieTryingToCreateReview()
    {
        var gameId = Guid.NewGuid();
        _repository.GetUserPostCountAsync(_currentUserId).Returns(50); // Newbie

        var act = async () => await _service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowForbiddenWhenUserHasNoPostsInGame()
    {
        var gameId = Guid.NewGuid();
        _repository.GetUserPostCountAsync(_currentUserId).Returns(200);
        _repository.CanReviewGameAsync(_currentUserId, gameId).Returns(false);

        var act = async () => await _service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowConflictWhenReviewAlreadyExists()
    {
        var gameId = Guid.NewGuid();
        _repository.GetUserPostCountAsync(_currentUserId).Returns(200);
        _repository.CanReviewGameAsync(_currentUserId, gameId).Returns(true);
        _repository.ExistsAsync(_currentUserId, gameId).Returns(true);

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
        await _repository.Received(1).CreateAsync(Arg.Any<CreateGameReviewEntity>());
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task ThrowNotFoundWhenReviewDoesNotExist()
    {
        var reviewId = Guid.NewGuid();
        _repository.GetAsync(reviewId).Returns((GameReview?)null);

        var act = async () => await _service.GetAsync(reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnReviewWhenExists()
    {
        var reviewId = Guid.NewGuid();
        var review = new GameReview { Id = reviewId, Text = "Great game!" };
        _repository.GetAsync(reviewId).Returns(review);

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
        _repository.GetAsync(reviewId).Returns(review);
        _repository.UpdateAsync(Arg.Any<UpdateGameReviewEntity>()).Returns(review);

        await _service.UpdateAsync(new UpdateGameReview { ReviewId = reviewId, Text = "Updated text" });

        _intentionManager.Received(1).ThrowIfForbidden(GameReviewIntention.Edit, review);
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
        _repository.GetAsync(reviewId).Returns(review);

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
        _repository.GetAsync(reviewId).Returns(review);

        var result = await _service.UpdateAsync(new UpdateGameReview { ReviewId = reviewId, Text = "" });

        result.Should().Be(review);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<UpdateGameReviewEntity>());
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
        _repository.GetAsync(reviewId).Returns(review);
        _repository.UpdateAsync(Arg.Any<UpdateGameReviewEntity>()).Returns(review);

        await _service.DeleteAsync(reviewId);

        _intentionManager.Received(1).ThrowIfForbidden(GameReviewIntention.Delete, review);
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
        _repository.GetAsync(reviewId).Returns(review);
        _repository.UpdateAsync(Arg.Any<UpdateGameReviewEntity>()).Returns(review);

        await _service.DeleteAsync(reviewId);

        await _repository.Received(1).UpdateAsync(Arg.Is<UpdateGameReviewEntity>(e => e.IsRemoved == true));
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
        _repository.CanReviewGameAsync(_currentUserId, gameId).Returns(true);

        var result = await _service.CanReviewAsync(_currentUserId, gameId);

        result.Should().BeTrue();
        await _repository.Received(1).CanReviewGameAsync(_currentUserId, gameId);
    }

    #endregion

    private void SetupSuccessfulCreate(Guid gameId, GameReview? expectedReview = null)
    {
        expectedReview ??= new GameReview { Id = Guid.NewGuid(), GameId = gameId, Text = "Great game!" };

        _repository.GetUserPostCountAsync(_currentUserId).Returns(200);
        _repository.CanReviewGameAsync(_currentUserId, gameId).Returns(true);
        _repository.ExistsAsync(_currentUserId, gameId).Returns(false);
        _repository.CreateAsync(Arg.Any<CreateGameReviewEntity>()).Returns(expectedReview);
    }
}
