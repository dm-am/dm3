using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using FluentValidation;

namespace DM.Domain.Game.Features.GameReviews;

/// <inheritdoc />
internal class GameReviewService : IGameReviewService
{
    private static readonly TimeSpan EditWindow = TimeSpan.FromDays(1);
    private const string ReviewNotFound = "Рецензия не найдена";

    private readonly IValidator<CreateGameReview> _createValidator;
    private readonly IValidator<UpdateGameReview> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameReviewRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGameService _gameService;

    public GameReviewService(
        IValidator<CreateGameReview> createValidator,
        IValidator<UpdateGameReview> updateValidator,
        IIntentionManager intentionManager,
        IGameReviewRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IGameService gameService)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _gameService = gameService;
    }

    /// <inheritdoc />
    public async Task<GameReview> CreateAsync(CreateGameReview createReview)
    {
        await _createValidator.ValidateAndThrowAsync(createReview);
        _intentionManager.ThrowIfForbidden(GameReviewIntention.Create);

        var author = _identityProvider.Current.User;
        var authorId = author.UserId;
        var gameId = createReview.GameId;
        var game = await _gameService.GetAsync(gameId);

        // MayCreateAsync asks the same, so the form and this refusal cannot part.
        var refusal = await RefusalAsync(authorId, game);
        if (refusal != null)
        {
            throw refusal;
        }

        var entity = new CreateGameReviewEntity
        {
            ReviewId = _guidFactory.Create(),
            UserId = authorId,
            GameId = gameId,
            CreatedUtc = _dateTimeProvider.Now,
            Text = ModBlockSanitizer.SanitizeForAuthor(createReview.Text, author.Role)
        };

        try
        {
            return await _repository.CreateAsync(entity);
        }
        catch (DuplicateEntityException)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyReviewedGame);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A guest stops at the intention and costs no query.
    /// </remarks>
    public async Task<bool> MayCreateAsync(Games.Game game)
    {
        if (!_intentionManager.IsAllowed(GameReviewIntention.Create))
        {
            return false;
        }

        return await RefusalAsync(_identityProvider.Current.User.UserId, game) == null;
    }

    /// <summary>
    /// Why this author may not review this game, or null; in the order the reader is told them.
    /// </summary>
    private async Task<HttpException?> RefusalAsync(Guid authorId, Games.Game game)
    {
        // A review is the word on a story told to its end, whatever ended it.
        if (game.Status != ModuleStatus.Closed)
        {
            return new HttpException(HttpStatusCode.Forbidden, "Рецензию можно написать только на закрытую игру");
        }

        var gameId = game.Id;
        if (await IsNewbieAsync(authorId))
        {
            return new HttpException(HttpStatusCode.Forbidden,
                $"Писать рецензии можно после {ProbationPolicy.NewbiePostThreshold} постов в играх");
        }

        if (!await CanReviewAsync(authorId, gameId))
        {
            return new HttpException(HttpStatusCode.Forbidden,
                "Рецензию можно написать только на игру, где у вас есть хотя бы один пост");
        }

        if (await _repository.ExistsAsync(authorId, gameId))
        {
            return new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyReviewedGame);
        }

        return null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The game is read first under the reader's scope, so a hidden game answers as a missing one.
    /// </remarks>
    public async Task<GameReview> GetAsync(Guid gameId, Guid reviewId)
    {
        await _gameService.GetAsync(gameId);

        var review = await FindAsync(reviewId);
        if (review.GameId != gameId)
        {
            throw new HttpException(HttpStatusCode.NotFound, ReviewNotFound);
        }

        return review;
    }

    /// <summary>
    /// No scope of its own: editing and removal carry their own gates.
    /// </summary>
    private async Task<GameReview> FindAsync(Guid id) =>
        await _repository.GetAsync(id) ?? throw new HttpException(HttpStatusCode.NotFound, ReviewNotFound);

    /// <inheritdoc />
    public async Task<(IEnumerable<GameReview> Reviews, PagingResult Paging)> GetListAsync(
        Guid gameId, PagingQuery query)
    {
        await _gameService.GetAsync(gameId);

        var totalCount = await _repository.CountAsync(gameId);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.PageSize,
            totalCount);

        var reviews = await _repository.GetAsync(gameId, pagingData);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<GameReview> Reviews, PagingResult Paging)> GetAllAsync(
        PagingQuery query, GameReviewFilter? filter = null)
    {
        // Only games the reader may open: the list names the game and quotes the text.
        var viewerId = _identityProvider.Current.User.UserId;
        var totalCount = await _repository.CountAllAsync(viewerId, filter);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.PageSize,
            totalCount);

        var reviews = await _repository.GetAllAsync(viewerId, pagingData, filter);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<GameReview> UpdateAsync(UpdateGameReview updateReview)
    {
        await _updateValidator.ValidateAndThrowAsync(updateReview);
        var review = await FindAsync(updateReview.ReviewId);

        _intentionManager.ThrowIfForbidden(GameReviewIntention.Edit, review);

        var currentUser = _identityProvider.Current.User;
        // MayEdit asks the same, so the pencil and this refusal cannot part.
        if (!IsWithinEditWindow(review))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Рецензию можно править в течение суток после отправки");
        }

        if (string.IsNullOrEmpty(updateReview.Text))
        {
            return review;
        }

        var entity = new UpdateGameReviewEntity(
            review.Id,
            Text: ModBlockSanitizer.SanitizeForAuthor(updateReview.Text, currentUser.Role),
            ModifiedUtc: _dateTimeProvider.Now,
            ModifiedByUserId: currentUser.UserId);

        return await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var review = await FindAsync(id);
        _intentionManager.ThrowIfForbidden(GameReviewIntention.Delete, review);

        // A senior moderator may remove somebody else's review, so the hand is recorded.
        var entity = new UpdateGameReviewEntity(
            id,
            IsRemoved: true,
            DeletedUtc: _dateTimeProvider.Now,
            DeletedByUserId: _identityProvider.Current.User.UserId);
        await _repository.UpdateAsync(entity);
    }

    /// <summary>
    /// The user has a post in the game
    /// </summary>
    private Task<bool> CanReviewAsync(Guid userId, Guid gameId) =>
        _repository.CanReviewGameAsync(userId, gameId);

    /// <inheritdoc />
    public bool MayEdit(GameReview review) =>
        _intentionManager.IsAllowed(GameReviewIntention.Edit, review) && IsWithinEditWindow(review);

    /// <inheritdoc />
    public bool MayDelete(GameReview review) => _intentionManager.IsAllowed(GameReviewIntention.Delete, review);

    /// <summary>
    /// An admin edits past the window.
    /// </summary>
    private bool IsWithinEditWindow(GameReview review) =>
        _identityProvider.Current.User.Role == UserRole.Admin ||
        _dateTimeProvider.Now <= review.CreatedUtc + EditWindow;

    private async Task<bool> IsNewbieAsync(Guid userId) =>
        ProbationPolicy.IsNewbie(await _repository.GetUserPostCountAsync(userId));
}
