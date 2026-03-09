using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Community.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Reviews;
using FluentValidation;
using Npgsql;

namespace DM.Domain.Community.Features.UserReviews;

/// <inheritdoc />
internal class UserReviewService : IUserReviewService
{
    private static readonly TimeSpan EditWindow = TimeSpan.FromDays(1);

    private readonly IValidator<CreateUserReview> _createValidator;
    private readonly IValidator<UpdateUserReview> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IUserReviewRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IProbationConfiguration _probationConfig;

    public UserReviewService(
        IValidator<CreateUserReview> createValidator,
        IValidator<UpdateUserReview> updateValidator,
        IIntentionManager intentionManager,
        IUserReviewRepository repository,
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
    public async Task<Review> CreateAsync(CreateUserReview createReview)
    {
        await _createValidator.ValidateAndThrowAsync(createReview);
        _intentionManager.ThrowIfForbidden(ReviewIntention.CreateUserReview);

        var authorId = _identityProvider.Current.User.UserId;
        var targetUserId = createReview.TargetUserId;

        // Newbies cannot create user reviews
        if (await IsNewbieAsync(authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You need at least 100 game posts to create user reviews");
        }

        // Can't review yourself
        if (authorId == targetUserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You cannot review yourself");
        }

        // Check if users have played together
        var havePlayedTogether = await HavePlayedTogetherAsync(authorId, targetUserId);
        if (!havePlayedTogether)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You can only review users you have played with in the same game");
        }

        // Check if already reviewed
        if (await ExistsAsync(authorId, targetUserId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already reviewed this user");
        }

        var entity = new CreateUserReviewEntity
        {
            ReviewId = _guidFactory.Create(),
            UserId = authorId,
            TargetUserId = targetUserId,
            CreatedUtc = _dateTimeProvider.Now,
            Text = createReview.Text.Trim()
        };

        try
        {
            return await _repository.CreateAsync(entity);
        }
        catch (Exception ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already reviewed this user");
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
        Guid targetUserId, PagingQuery query)
    {
        var totalCount = await _repository.CountAsync(targetUserId);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var reviews = await _repository.GetAsync(targetUserId, pagingData);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> Reviews, PagingResult Paging)> GetAllAsync(
        PagingQuery query, UserReviewFilter? filter = null)
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
    public Task<Review?> GetByAuthorAsync(Guid targetUserId, Guid authorId) =>
        _repository.GetByAuthorAsync(targetUserId, authorId);

    /// <inheritdoc />
    public async Task<Review> UpdateAsync(UpdateUserReview updateReview)
    {
        await _updateValidator.ValidateAndThrowAsync(updateReview);
        var review = await GetAsync(updateReview.ReviewId);

        _intentionManager.ThrowIfForbidden(ReviewIntention.Edit, review);

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

        var entity = new UpdateUserReviewEntity(
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
        _intentionManager.ThrowIfForbidden(ReviewIntention.Delete, review);

        var entity = new UpdateUserReviewEntity(id, IsRemoved: true);
        await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid targetUserId) =>
        _repository.ExistsAsync(authorId, targetUserId);

    /// <inheritdoc />
    public Task<bool> HavePlayedTogetherAsync(Guid userId1, Guid userId2) =>
        _repository.HavePlayedTogetherAsync(userId1, userId2);

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
