using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Game.Features.Games;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Tests.Features.GameReviews;

public class GameReviewServiceShould : UnitTestBase
{
    private readonly IValidator<CreateGameReview> createValidator;
    private readonly IValidator<UpdateGameReview> updateValidator;
    private readonly IIntentionManager intentionManager;
    private readonly IGameReviewRepository repository;
    private readonly IIdentityProvider identityProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IGameService gameService;
    private readonly GameReviewService service;
    private readonly Guid currentUserId;

    public GameReviewServiceShould()
    {
        createValidator = Mock<IValidator<CreateGameReview>>();
        createValidator.ValidateAsync(Arg.Any<CreateGameReview>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        updateValidator = Mock<IValidator<UpdateGameReview>>();
        updateValidator.ValidateAsync(Arg.Any<UpdateGameReview>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        intentionManager = Mock<IIntentionManager>();

        repository = Mock<IGameReviewRepository>();

        currentUserId = Guid.NewGuid();
        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId, UserRole.RegularUser));

        guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        gameService = Mock<IGameService>();
        gameService.GetAsync(Arg.Any<Guid>()).Returns(call => ClosedGame(call.Arg<Guid>()));

        service = new GameReviewService(
            createValidator,
            updateValidator,
            intentionManager,
            repository,
            identityProvider,
            guidFactory,
            dateTimeProvider,
            gameService);
    }

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var gameId = Guid.NewGuid();
        SetupSuccessfulCreate(gameId);

        await service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        intentionManager.Received(1).ThrowIfForbidden(GameReviewIntention.Create);
    }

    [Fact]
    public async Task ThrowForbiddenWhenNewbieTryingToCreateReview()
    {
        var gameId = Guid.NewGuid();
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold - 1);

        var act = async () => await service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowForbiddenWhenUserHasNoPostsInGame()
    {
        var gameId = Guid.NewGuid();
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold);
        repository.CanReviewGameAsync(currentUserId, gameId).Returns(false);

        var act = async () => await service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowConflictWhenReviewAlreadyExists()
    {
        var gameId = Guid.NewGuid();
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold);
        repository.CanReviewGameAsync(currentUserId, gameId).Returns(true);
        repository.ExistsAsync(currentUserId, gameId).Returns(true);

        var act = async () => await service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateReviewSuccessfully()
    {
        var gameId = Guid.NewGuid();
        var expectedReview = new GameReview { Id = Guid.NewGuid(), GameId = gameId, Text = "Great game!" };
        SetupSuccessfulCreate(gameId, expectedReview);

        var result = await service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        result.Should().Be(expectedReview);
        await repository.Received(1).CreateAsync(Arg.Any<CreateGameReviewEntity>());
    }

    [Fact]
    public async Task ThrowNotFoundWhenReviewDoesNotExist()
    {
        var gameId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        repository.GetAsync(reviewId).Returns((GameReview?)null);

        var act = async () => await service.GetAsync(gameId, reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnReviewWhenExists()
    {
        var gameId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var review = new GameReview { Id = reviewId, GameId = gameId, Text = "Great game!" };
        repository.GetAsync(reviewId).Returns(review);

        var result = await service.GetAsync(gameId, reviewId);

        result.Should().Be(review);
        await gameService.Received(1).GetAsync(gameId);
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
        repository.GetAsync(reviewId).Returns(new GameReview { Id = reviewId, GameId = gameId, Text = "Тайное" });
        gameService.GetAsync(gameId).ThrowsAsync(new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound));

        var act = async () => await service.GetAsync(gameId, reviewId);

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
        repository.GetAsync(reviewId).Returns(new GameReview { Id = reviewId, GameId = Guid.NewGuid(), Text = "Чужое" });

        var act = async () => await service.GetAsync(gameId, reviewId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == "Рецензия не найдена");
    }

    [Fact]
    public async Task AuthorizeEditAction()
    {
        var reviewId = Guid.NewGuid();
        var review = new GameReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = currentUserId },
            Text = "Original text",
            CreatedUtc = DateTimeOffset.UtcNow
        };
        repository.GetAsync(reviewId).Returns(review);
        repository.UpdateAsync(Arg.Any<UpdateGameReviewEntity>()).Returns(review);

        await service.UpdateAsync(new UpdateGameReview { ReviewId = reviewId, Text = "Updated text" });

        intentionManager.Received(1).ThrowIfForbidden(GameReviewIntention.Edit, review);
    }

    [Fact]
    public async Task ThrowForbiddenWhenEditWindowExpired()
    {
        var reviewId = Guid.NewGuid();
        var review = new GameReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = currentUserId },
            Text = "Original text",
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2) // Past edit window
        };
        repository.GetAsync(reviewId).Returns(review);

        var act = async () => await service.UpdateAsync(new UpdateGameReview { ReviewId = reviewId, Text = "Updated text" });

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
            Author = new GeneralUser { UserId = currentUserId },
            Text = "Original text",
            CreatedUtc = DateTimeOffset.UtcNow
        };
        repository.GetAsync(reviewId).Returns(review);

        var result = await service.UpdateAsync(new UpdateGameReview { ReviewId = reviewId, Text = "" });

        result.Should().Be(review);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<UpdateGameReviewEntity>());
    }

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var reviewId = Guid.NewGuid();
        var review = new GameReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = currentUserId },
            Text = "Some text"
        };
        repository.GetAsync(reviewId).Returns(review);
        repository.UpdateAsync(Arg.Any<UpdateGameReviewEntity>()).Returns(review);

        await service.DeleteAsync(reviewId);

        intentionManager.Received(1).ThrowIfForbidden(GameReviewIntention.Delete, review);
    }

    [Fact]
    public async Task SoftDeleteReview()
    {
        var reviewId = Guid.NewGuid();
        var review = new GameReview
        {
            Id = reviewId,
            Author = new GeneralUser { UserId = currentUserId },
            Text = "Some text"
        };
        repository.GetAsync(reviewId).Returns(review);
        repository.UpdateAsync(Arg.Any<UpdateGameReviewEntity>()).Returns(review);

        await service.DeleteAsync(reviewId);

        await repository.Received(1).UpdateAsync(Arg.Is<UpdateGameReviewEntity>(e =>
            e.IsRemoved == true && e.DeletedByUserId == currentUserId && e.DeletedUtc != null));
    }

    /// <summary>
    /// The pencil is offered by this answer: the intention and the day the edit itself is refused by.
    /// </summary>
    [Theory]
    [InlineData(true, 12, UserRole.RegularUser, true)]
    [InlineData(true, 48, UserRole.RegularUser, false)]
    [InlineData(true, 48, UserRole.Admin, true)]
    [InlineData(false, 12, UserRole.Admin, false)]
    public void OfferTheEditOnlyWhereTheWriteTakesIt(bool allowed, int ageHours, UserRole role, bool expected)
    {
        identityProvider.Current.Returns(Identities.User(currentUserId, role));
        var review = new GameReview { CreatedUtc = DateTimeOffset.UtcNow.AddHours(-ageHours) };
        intentionManager.IsAllowed(GameReviewIntention.Edit, review).Returns(allowed);

        service.MayEdit(review).Should().Be(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OfferTheDeleteWhereTheIntentionAllowsIt(bool allowed)
    {
        var review = new GameReview();
        intentionManager.IsAllowed(GameReviewIntention.Delete, review).Returns(allowed);

        service.MayDelete(review).Should().Be(allowed);
    }

    /// <summary>
    /// The flag rides on every read of a game's details, so a guest, whom the
    /// intention already refuses, costs no query at all.
    /// </summary>
    [Fact]
    public async Task AnswerAGuestWithoutAskingTheDatabase()
    {
        var gameId = Guid.NewGuid();
        intentionManager.IsAllowed(GameReviewIntention.Create).Returns(false);

        var result = await service.MayCreateAsync(ClosedGame(gameId));

        result.Should().BeFalse();
        await repository.DidNotReceiveWithAnyArgs().GetUserPostCountAsync(default);
        await repository.DidNotReceiveWithAnyArgs().CanReviewGameAsync(default, default);
        await repository.DidNotReceiveWithAnyArgs().ExistsAsync(default, default);
    }

    [Theory]
    [InlineData(50, true, false, false)]
    [InlineData(200, false, false, false)]
    [InlineData(200, true, true, false)]
    [InlineData(200, true, false, true)]
    public async Task OfferTheReviewOnlyWhereTheWriteTakesIt(int posts, bool hasPostInGame, bool reviewed, bool expected)
    {
        var gameId = Guid.NewGuid();
        intentionManager.IsAllowed(GameReviewIntention.Create).Returns(true);
        repository.GetUserPostCountAsync(currentUserId).Returns(posts);
        repository.CanReviewGameAsync(currentUserId, gameId).Returns(hasPostInGame);
        repository.ExistsAsync(currentUserId, gameId).Returns(reviewed);

        var result = await service.MayCreateAsync(ClosedGame(gameId));

        result.Should().Be(expected);
    }

    /// <summary>
    /// A review is written on a closed game, whatever closed it; the form and the
    /// write answer alike.
    /// </summary>
    [Theory]
    [InlineData(ModuleStatus.Draft, ClosedReason.None, false)]
    [InlineData(ModuleStatus.Active, ClosedReason.None, false)]
    [InlineData(ModuleStatus.Closed, ClosedReason.Finished, true)]
    [InlineData(ModuleStatus.Closed, ClosedReason.Frozen, true)]
    [InlineData(ModuleStatus.Closed, ClosedReason.None, true)]
    public async Task OfferTheReviewOnlyOnAClosedGame(ModuleStatus status, ClosedReason reason, bool expected)
    {
        var gameId = Guid.NewGuid();
        intentionManager.IsAllowed(GameReviewIntention.Create).Returns(true);
        SetupSuccessfulCreate(gameId);
        var game = new GameDto { Id = gameId, Status = status, ClosedReason = reason };

        (await service.MayCreateAsync(game)).Should().Be(expected);
    }

    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Active)]
    public async Task RefuseAReviewOfAGameThatIsNotClosed(ModuleStatus status)
    {
        var gameId = Guid.NewGuid();
        SetupSuccessfulCreate(gameId);
        gameService.GetAsync(gameId).Returns(new GameDto { Id = gameId, Status = status });

        var act = async () => await service.CreateAsync(new CreateGameReview { GameId = gameId, Text = "Great game!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden &&
                        e.Message == "Рецензию можно написать только на закрытую игру");
        await repository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    private static GameDto ClosedGame(Guid gameId) =>
        new() { Id = gameId, Status = ModuleStatus.Closed, ClosedReason = ClosedReason.Finished };

    private void SetupSuccessfulCreate(Guid gameId, GameReview? expectedReview = null)
    {
        expectedReview ??= new GameReview { Id = Guid.NewGuid(), GameId = gameId, Text = "Great game!" };

        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold);
        repository.CanReviewGameAsync(currentUserId, gameId).Returns(true);
        repository.ExistsAsync(currentUserId, gameId).Returns(false);
        repository.CreateAsync(Arg.Any<CreateGameReviewEntity>()).Returns(expectedReview);
    }
}
