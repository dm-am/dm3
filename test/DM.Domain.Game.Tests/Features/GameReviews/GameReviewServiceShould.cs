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
using DM.Testing.Dsl;
using DM.Testing;
using DM.Testing.Markup;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
    private readonly IGameService _gameService;
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

        _gameService = Mock<IGameService>();

        _service = new GameReviewService(
            _createValidator,
            _updateValidator,
            _intentionManager,
            _repository,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider,
            new TagNamesAsTyped(),
            _gameService);
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
        var gameId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        _repository.GetAsync(reviewId).Returns((GameReview?)null);

        var act = async () => await _service.GetAsync(gameId, reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnReviewWhenExists()
    {
        var gameId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var review = new GameReview { Id = reviewId, GameId = gameId, Text = "Great game!" };
        _repository.GetAsync(reviewId).Returns(review);

        var result = await _service.GetAsync(gameId, reviewId);

        result.Should().Be(review);
        await _gameService.Received(1).GetAsync(gameId);
    }

    /// <summary>
    /// A game the reader may not open answers as a game that is not there, before
    /// the review is looked at: its reviews are as hidden as the game.
    /// </summary>
    [Fact]
    public async Task AnswerAReviewOfAHiddenGameAsTheGameNotBeingThere()
    {
        var gameId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        _repository.GetAsync(reviewId).Returns(new GameReview { Id = reviewId, GameId = gameId, Text = "Тайное" });
        _gameService.GetAsync(gameId).ThrowsAsync(new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound));

        var act = async () => await _service.GetAsync(gameId, reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == RefusalMessage.GameNotFound);
    }

    /// <summary>
    /// A review of another game, reached through this game's address, is not
    /// this game's review.
    /// </summary>
    [Fact]
    public async Task AnswerAReviewOfAnotherGameAsAReviewThatIsNotThere()
    {
        var gameId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        _repository.GetAsync(reviewId).Returns(new GameReview { Id = reviewId, GameId = Guid.NewGuid(), Text = "Чужое" });

        var act = async () => await _service.GetAsync(gameId, reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == "Рецензия не найдена");
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

        await _repository.Received(1).UpdateAsync(Arg.Is<UpdateGameReviewEntity>(e =>
            e.IsRemoved == true && e.DeletedByUserId == _currentUserId && e.DeletedUtc != null));
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

    #region MayCreate Tests

    /// <summary>
    /// The flag rides on every read of a game's details, so a guest, whom the
    /// intention already refuses, costs no query at all.
    /// </summary>
    [Fact]
    public async Task AnswerAGuestWithoutAskingTheDatabase()
    {
        var gameId = Guid.NewGuid();
        _intentionManager.IsAllowed(GameReviewIntention.Create).Returns(false);

        var result = await _service.MayCreateAsync(gameId);

        result.Should().BeFalse();
        await _repository.DidNotReceiveWithAnyArgs().GetUserPostCountAsync(default);
        await _repository.DidNotReceiveWithAnyArgs().CanReviewGameAsync(default, default);
        await _repository.DidNotReceiveWithAnyArgs().ExistsAsync(default, default);
    }

    [Theory]
    [InlineData(50, true, false, false)]
    [InlineData(200, false, false, false)]
    [InlineData(200, true, true, false)]
    [InlineData(200, true, false, true)]
    public async Task OfferTheReviewOnlyWhereTheWriteTakesIt(int posts, bool hasPostInGame, bool reviewed, bool expected)
    {
        var gameId = Guid.NewGuid();
        _intentionManager.IsAllowed(GameReviewIntention.Create).Returns(true);
        _repository.GetUserPostCountAsync(_currentUserId).Returns(posts);
        _repository.CanReviewGameAsync(_currentUserId, gameId).Returns(hasPostInGame);
        _repository.ExistsAsync(_currentUserId, gameId).Returns(reviewed);

        var result = await _service.MayCreateAsync(gameId);

        result.Should().Be(expected);
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
