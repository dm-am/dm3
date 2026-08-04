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
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Games;
using FluentValidation;

namespace DM.Domain.Game.Features.PostReviews;

/// <inheritdoc />
internal class PostReviewService : IPostReviewService
{
    private static readonly TimeSpan EditWindow = TimeSpan.FromDays(1);
    private static readonly TimeSpan CooldownPerGame = TimeSpan.FromDays(3);

    private readonly IValidator<CreatePostReview> _createValidator;
    private readonly IValidator<UpdatePostReview> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IPostReviewRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGameBlacklistRepository _blacklistRepository;

    public PostReviewService(
        IValidator<CreatePostReview> createValidator,
        IValidator<UpdatePostReview> updateValidator,
        IIntentionManager intentionManager,
        IPostReviewRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IGameBlacklistRepository blacklistRepository)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _blacklistRepository = blacklistRepository;
    }

    /// <inheritdoc />
    public async Task<PostReview> CreateAsync(CreatePostReview createReview)
    {
        await _createValidator.ValidateAndThrowAsync(createReview);
        _intentionManager.ThrowIfForbidden(PostReviewIntention.Create);

        var author = _identityProvider.Current.User;
        var authorId = author.UserId;
        var postId = createReview.PostId;

        // Get post information for authorization and denormalization
        var postInfo = await _repository.GetPostInfoAsync(postId);
        if (postInfo == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        // The blacklist closes writing, and rating a post writes into the game it
        // belongs to. The game's rooms and posts are open to a blacklisted reader
        // and stay open, so the refusal has to be here and not in what they see.
        if (await _blacklistRepository.IsBlocked(postInfo.GameId, authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromGame);
        }

        // Can't review own post
        if (authorId == postInfo.AuthorId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя оценивать собственный пост");
        }

        // Newbies can only create neutral post reviews
        if (createReview.Sign != ReviewSign.Neutral && await IsNewbieAsync(authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                $"Ставить плюс и минус можно после {ProbationPolicy.NewbiePostThreshold} постов в играх");
        }

        // Check if already reviewed this post
        if (await ExistsAsync(authorId, postId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyReviewedPost);
        }

        // Check cooldown: can't review posts in the same game within 3 days
        if (await HasRecentReviewInGameAsync(authorId, postInfo.GameId))
        {
            throw new HttpException(HttpStatusCode.TooManyRequests,
                "Оценивать посты в одной игре можно раз в три дня");
        }

        var entity = new CreatePostReviewEntity
        {
            PostReviewId = _guidFactory.Create(),
            AuthorId = authorId,
            PostId = postId,
            PostAuthorId = postInfo.AuthorId,
            GameId = postInfo.GameId,
            CreatedUtc = _dateTimeProvider.Now,
            Sign = createReview.Sign,
            // Review bodies render on the Comment surface where [mod] is a green
            // mod block; strip it when authored by a non-moderator.
            Text = ModBlockSanitizer.SanitizeForAuthor(createReview.Text, author.Role)
        };

        try
        {
            var result = await _repository.CreateAsync(entity);

            // Update post author's QualityRating based on review sign
            if (createReview.Sign != ReviewSign.Neutral)
            {
                var signValue = (int)createReview.Sign;
                await _repository.UpdateUserQualityRatingAsync(postInfo.AuthorId, signValue);
            }

            return result;
        }
        catch (DuplicateEntityException)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyReviewedPost);
        }
    }

    /// <inheritdoc />
    public async Task<PostReview> GetAsync(Guid id)
    {
        var review = await _repository.GetAsync(id);
        if (review == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Оценка не найдена");
        }

        return review;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<PostReview> Reviews, PagingResult Paging)> GetListAsync(
        Guid postId, PagingQuery query)
    {
        var totalCount = await _repository.CountAsync(postId);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var reviews = await _repository.GetAsync(postId, pagingData);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<PostReview> Reviews, PagingResult Paging)> GetAllAsync(
        PagingQuery query, PostReviewFilter? filter = null)
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
    public Task<PostReview?> GetByAuthorAsync(Guid postId, Guid authorId) =>
        _repository.GetByAuthorAsync(postId, authorId);

    /// <inheritdoc />
    public async Task<PostReview> UpdateAsync(UpdatePostReview updateReview)
    {
        await _updateValidator.ValidateAndThrowAsync(updateReview);
        var review = await GetAsync(updateReview.ReviewId);

        _intentionManager.ThrowIfForbidden(PostReviewIntention.Edit, review);

        // Check 24-hour edit window (admins can edit anytime)
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role != UserRole.Admin && !CanEdit(review))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Оценку можно править в течение суток после публикации");
        }

        // Handle sign change impact on QualityRating
        var oldSign = review.Sign;
        var newSign = updateReview.Sign ?? oldSign;

        if (newSign != oldSign)
        {
            // Revert old sign effect
            if (oldSign != ReviewSign.Neutral)
            {
                await _repository.UpdateUserQualityRatingAsync(review.PostAuthor.UserId, -(int)oldSign);
            }
            // Apply new sign effect
            if (newSign != ReviewSign.Neutral)
            {
                await _repository.UpdateUserQualityRatingAsync(review.PostAuthor.UserId, (int)newSign);
            }
        }

        var entity = new UpdatePostReviewEntity(
            review.Id,
            Sign: updateReview.Sign,
            ModifiedUtc: _dateTimeProvider.Now,
            ModifiedByUserId: currentUser.UserId);

        return await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var review = await GetAsync(id);
        _intentionManager.ThrowIfForbidden(PostReviewIntention.Delete, review);

        // Revert post author's QualityRating when post review is deleted
        if (review.Sign != ReviewSign.Neutral)
        {
            var signValue = (int)review.Sign;
            await _repository.UpdateUserQualityRatingAsync(review.PostAuthor.UserId, -signValue);
        }

        var entity = new UpdatePostReviewEntity(id, IsRemoved: true);
        await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid postId) =>
        _repository.ExistsAsync(authorId, postId);

    /// <inheritdoc />
    public Task<bool> HasRecentReviewInGameAsync(Guid authorId, Guid gameId)
    {
        var cutoffDate = _dateTimeProvider.Now - CooldownPerGame;
        return _repository.HasRecentReviewInGameAsync(authorId, gameId, cutoffDate);
    }

    /// <inheritdoc />
    public bool CanEdit(PostReview review)
    {
        var now = _dateTimeProvider.Now;
        var editDeadline = review.CreatedUtc + EditWindow;
        return now <= editDeadline;
    }

    private async Task<bool> IsNewbieAsync(Guid userId) =>
        ProbationPolicy.IsNewbie(await _repository.GetUserPostCountAsync(userId));
}
