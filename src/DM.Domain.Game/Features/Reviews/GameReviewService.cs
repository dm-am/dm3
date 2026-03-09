using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Reviews;
using DM.Domain.Game.Authorization;
using FluentValidation;
using Npgsql;

namespace DM.Domain.Game.Features.Reviews;

/// <inheritdoc />
internal class GameReviewService : IGameReviewService
{
    private static readonly TimeSpan EditWindow = TimeSpan.FromDays(1);

    private readonly IValidator<CreateGameReview> _createValidator;
    private readonly IValidator<UpdateGameReview> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameReviewRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IProbationConfiguration _probationConfig;

    public GameReviewService(
        IValidator<CreateGameReview> createValidator,
        IValidator<UpdateGameReview> updateValidator,
        IIntentionManager intentionManager,
        IGameReviewRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IProbationConfiguration probationConfig)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _probationConfig = probationConfig;
    }

    /// <inheritdoc />
    public async Task<Review> CreateAsync(CreateGameReview createReview)
    {
        await _createValidator.ValidateAndThrowAsync(createReview);
        _intentionManager.ThrowIfForbidden(GameReviewIntention.Create);

        var authorId = _identityProvider.Current.User.UserId;
        var gameId = createReview.GameId;

        // Newbies cannot create game reviews
        if (await IsNewbieAsync(authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You need at least 100 game posts to create game reviews");
        }

        // Check if user can review this game (has at least one post in the game)
        var canReview = await CanReviewAsync(authorId, gameId);
        if (!canReview)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You can only review games where you have at least one post");
        }

        // Check if already reviewed
        if (await ExistsAsync(authorId, gameId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already reviewed this game");
        }

        var entity = new CreateGameReviewEntity
        {
            ReviewId = _guidFactory.Create(),
            UserId = authorId,
            GameId = gameId,
            CreatedUtc = _dateTimeProvider.Now,
            Text = createReview.Text.Trim()
        };

        try
        {
            return await _repository.CreateAsync(entity);
        }
        catch (Exception ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already reviewed this game");
        }
    }

    /// <inheritdoc />
    public async Task<Review> GetAsync(Guid id)
    {
        var review = await _repository.GetAsync(id);
        if (review == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Review not found");
        }

        return review;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> Reviews, PagingResult Paging)> GetListAsync(
        Guid gameId, PagingQuery query)
    {
        var totalCount = await _repository.CountAsync(gameId);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var reviews = await _repository.GetAsync(gameId, pagingData);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> Reviews, PagingResult Paging)> GetAllAsync(
        PagingQuery query, GameReviewFilter? filter = null)
    {
        var totalCount = await _repository.CountAllAsync(filter);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var reviews = await _repository.GetAllAsync(pagingData, filter);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public Task<Review?> GetByAuthorAsync(Guid gameId, Guid authorId) =>
        _repository.GetByAuthorAsync(gameId, authorId);

    /// <inheritdoc />
    public async Task<Review> UpdateAsync(UpdateGameReview updateReview)
    {
        await _updateValidator.ValidateAndThrowAsync(updateReview);
        var review = await GetAsync(updateReview.ReviewId);

        _intentionManager.ThrowIfForbidden(GameReviewIntention.Edit, review);

        // Check 24-hour edit window (admins can edit anytime)
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role != UserRole.Admin && !CanEdit(review))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Reviews can only be edited within 24 hours of creation");
        }

        if (string.IsNullOrEmpty(updateReview.Text))
        {
            return review;
        }

        var entity = new UpdateGameReviewEntity(
            review.Id,
            Text: updateReview.Text.Trim(),
            ModifiedUtc: _dateTimeProvider.Now,
            ModifiedByUserId: currentUser.UserId);

        return await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var review = await GetAsync(id);
        _intentionManager.ThrowIfForbidden(GameReviewIntention.Delete, review);

        var entity = new UpdateGameReviewEntity(id, IsRemoved: true);
        await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid gameId) =>
        _repository.ExistsAsync(authorId, gameId);

    /// <inheritdoc />
    public Task<bool> CanReviewAsync(Guid userId, Guid gameId) =>
        _repository.CanReviewGameAsync(userId, gameId);

    /// <inheritdoc />
    public bool CanEdit(Review review)
    {
        var now = _dateTimeProvider.Now;
        var editDeadline = review.CreatedUtc + EditWindow;
        return now <= editDeadline;
    }

    private async Task<bool> IsNewbieAsync(Guid userId)
    {
        var postCount = await _repository.GetUserPostCountAsync(userId);
        return postCount < _probationConfig.NewbiePostThreshold;
    }
}
