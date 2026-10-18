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
    private readonly IBbTagNameCase _tagNames;
    private readonly IGameService _gameService;

    public GameReviewService(
        IValidator<CreateGameReview> createValidator,
        IValidator<UpdateGameReview> updateValidator,
        IIntentionManager intentionManager,
        IGameReviewRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IBbTagNameCase tagNames,
        IGameService gameService)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _tagNames = tagNames;
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

        // MayCreateAsync asks the same, so the form the page offers and this
        // refusal cannot part.
        var refusal = await RefusalAsync(authorId, gameId);
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
            // Review bodies render on the Comment surface where [mod] is a green
            // mod block; strip it when authored by a non-moderator.
            Text = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(createReview.Text.Trim(), author.Role), BbTagSet.WithModBlock)
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
    /// The intention, then the three refusals CreateAsync throws, through the
    /// one method both call. A guest stops at the intention and costs no query.
    /// </remarks>
    public async Task<bool> MayCreateAsync(Guid gameId)
    {
        if (!_intentionManager.IsAllowed(GameReviewIntention.Create))
        {
            return false;
        }

        return await RefusalAsync(_identityProvider.Current.User.UserId, gameId) == null;
    }

    /// <summary>
    /// Why this author may not review this game, or null when he may: the
    /// probation, a post of his in the game, a review of his already there. In
    /// that order, which is the order the reader is told them in.
    /// </summary>
    private async Task<HttpException?> RefusalAsync(Guid authorId, Guid gameId)
    {
        // Newbies cannot create game reviews
        if (await IsNewbieAsync(authorId))
        {
            return new HttpException(HttpStatusCode.Forbidden,
                $"Писать рецензии можно после {ProbationPolicy.NewbiePostThreshold} постов в играх");
        }

        // Check if user can review this game (has at least one post in the game)
        if (!await CanReviewAsync(authorId, gameId))
        {
            return new HttpException(HttpStatusCode.Forbidden,
                "Рецензию можно написать только на игру, где у вас есть хотя бы один пост");
        }

        // Check if already reviewed
        if (await ExistsAsync(authorId, gameId))
        {
            return new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyReviewedGame);
        }

        return null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The route names both, and the game is read first under the reader's
    /// scope, so a review of a game hidden from the reader answers as a game
    /// that is not there. A review of another game reached through this game's
    /// address is not this game's review.
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
    /// The review itself, with no scope of its own: editing and removal carry
    /// their own gates, the author and the moderator, and are not reads.
    /// </summary>
    private async Task<GameReview> FindAsync(Guid id) =>
        await _repository.GetAsync(id) ?? throw new HttpException(HttpStatusCode.NotFound, ReviewNotFound);

    /// <inheritdoc />
    public async Task<(IEnumerable<GameReview> Reviews, PagingResult Paging)> GetListAsync(
        Guid gameId, PagingQuery query)
    {
        // A game the reader may not open has no reviews to show: the read answers
        // as a game that is not there, the same 404 its address alone would.
        await _gameService.GetAsync(gameId);

        var totalCount = await _repository.CountAsync(gameId);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var reviews = await _repository.GetAsync(gameId, pagingData);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<GameReview> Reviews, PagingResult Paging)> GetAllAsync(
        PagingQuery query, GameReviewFilter? filter = null)
    {
        // Only the reviews of games the reader may open: the list names the game
        // and quotes the text, and a hidden game does not surface through it.
        var viewerId = _identityProvider.Current.User.UserId;
        var totalCount = await _repository.CountAllAsync(viewerId, filter);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var reviews = await _repository.GetAllAsync(viewerId, pagingData, filter);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public Task<GameReview?> GetByAuthorAsync(Guid gameId, Guid authorId) =>
        _repository.GetByAuthorAsync(gameId, authorId);

    /// <inheritdoc />
    public async Task<GameReview> UpdateAsync(UpdateGameReview updateReview)
    {
        await _updateValidator.ValidateAndThrowAsync(updateReview);
        var review = await FindAsync(updateReview.ReviewId);

        _intentionManager.ThrowIfForbidden(GameReviewIntention.Edit, review);

        // Check 24-hour edit window (admins can edit anytime)
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role != UserRole.Admin && !CanEdit(review))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Рецензию можно править в течение суток после публикации");
        }

        if (string.IsNullOrEmpty(updateReview.Text))
        {
            return review;
        }

        var entity = new UpdateGameReviewEntity(
            review.Id,
            // Review bodies render on the Comment surface where [mod] is a green
            // mod block; strip it when the editor is a non-moderator.
            Text: _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(updateReview.Text.Trim(), currentUser.Role), BbTagSet.WithModBlock),
            ModifiedUtc: _dateTimeProvider.Now,
            ModifiedByUserId: currentUser.UserId);

        return await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var review = await FindAsync(id);
        _intentionManager.ThrowIfForbidden(GameReviewIntention.Delete, review);

        // Who removed it and when: a senior moderator may remove somebody
        // else's review, and a removal with no hand behind it cannot be
        // reviewed afterwards.
        var entity = new UpdateGameReviewEntity(
            id,
            IsRemoved: true,
            DeletedUtc: _dateTimeProvider.Now,
            DeletedByUserId: _identityProvider.Current.User.UserId);
        await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid gameId) =>
        _repository.ExistsAsync(authorId, gameId);

    /// <summary>
    /// Check if user can review a game (has at least one post in it)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="gameId">Game ID</param>
    /// <returns>True if user can review the game</returns>
    public Task<bool> CanReviewAsync(Guid userId, Guid gameId) =>
        _repository.CanReviewGameAsync(userId, gameId);

    /// <inheritdoc />
    public bool CanEdit(GameReview review)
    {
        var now = _dateTimeProvider.Now;
        var editDeadline = review.CreatedUtc + EditWindow;
        return now <= editDeadline;
    }

    private async Task<bool> IsNewbieAsync(Guid userId) =>
        ProbationPolicy.IsNewbie(await _repository.GetUserPostCountAsync(userId));
}
