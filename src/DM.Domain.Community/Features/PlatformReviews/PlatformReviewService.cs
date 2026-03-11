using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Community.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Reviews;
using DM.Domain.Core.Users;
using FluentValidation;
using Npgsql;

namespace DM.Domain.Community.Features.PlatformReviews;

/// <inheritdoc />
internal class PlatformReviewService : IPlatformReviewService
{
    private readonly IValidator<CreatePlatformReview> _createValidator;
    private readonly IValidator<UpdatePlatformReview> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IPlatformReviewRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserLookupService _userLookupService;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PlatformReviewService(
        IValidator<CreatePlatformReview> createValidator,
        IValidator<UpdatePlatformReview> updateValidator,
        IIntentionManager intentionManager,
        IPlatformReviewRepository repository,
        IIdentityProvider identityProvider,
        IUserLookupService userLookupService,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _userLookupService = userLookupService;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Review> CreateAsync(CreatePlatformReview createReview)
    {
        await _createValidator.ValidateAndThrowAsync(createReview);
        _intentionManager.ThrowIfForbidden(ReviewIntention.Create);

        var authorId = _identityProvider.Current.User.UserId;
        if (!string.IsNullOrEmpty(createReview.AuthorUsername))
        {
            var author = await _userLookupService.GetAsync(createReview.AuthorUsername);
            authorId = author.UserId;
        }

        if (await _repository.UserHasReviewAsync(authorId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "User already has a review");
        }

        var entity = new CreatePlatformReviewEntity
        {
            ReviewId = _guidFactory.Create(),
            UserId = authorId,
            CreatedUtc = _dateTimeProvider.Now,
            Text = createReview.Text.Trim(),
            IsApproved = true // Platform reviews are auto-approved
        };

        try
        {
            return await _repository.CreateAsync(entity);
        }
        catch (Exception ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new HttpException(HttpStatusCode.Conflict, "User already has a review");
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

        // Platform reviews require approval check for non-admins
        if (!review.Approved && !_intentionManager.IsAllowed(ReviewIntention.ReadUnapproved))
        {
            throw new HttpException(HttpStatusCode.NotFound, "Review not found");
        }

        return review;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> Reviews, PagingResult Paging)> GetListAsync(
        PagingQuery query, bool onlyApproved)
    {
        onlyApproved = onlyApproved || !_intentionManager.IsAllowed(ReviewIntention.ReadUnapproved);
        var totalCount = await _repository.CountAsync(onlyApproved);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var reviews = await _repository.GetAsync(pagingData, onlyApproved);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Review> UpdateAsync(UpdatePlatformReview updateReview)
    {
        await _updateValidator.ValidateAndThrowAsync(updateReview);
        var review = await GetAsync(updateReview.ReviewId);

        var entity = new UpdatePlatformReviewEntity(review.Id);
        var hasChanges = false;

        if (updateReview.Approved.HasValue && updateReview.Approved.Value != review.Approved)
        {
            _intentionManager.ThrowIfForbidden(ReviewIntention.Approve, review);
            entity = entity with { IsApproved = updateReview.Approved };
            hasChanges = true;
        }

        if (!string.IsNullOrEmpty(updateReview.Text))
        {
            _intentionManager.ThrowIfForbidden(ReviewIntention.Edit, review);
            var currentUser = _identityProvider.Current.User;
            entity = entity with
            {
                Text = updateReview.Text.Trim(),
                ModifiedUtc = _dateTimeProvider.Now,
                ModifiedByUserId = currentUser.UserId
            };
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return review;
        }

        return await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var review = await GetAsync(id);
        _intentionManager.ThrowIfForbidden(ReviewIntention.Delete, review);

        var entity = new UpdatePlatformReviewEntity(id, IsRemoved: true);
        await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public async Task<Review> SetApprovalAsync(Guid id, bool approved)
    {
        var review = await GetAsync(id);
        _intentionManager.ThrowIfForbidden(ReviewIntention.Approve, review);

        if (review.Approved == approved)
        {
            return review;
        }

        var entity = new UpdatePlatformReviewEntity(id, IsApproved: approved);
        return await _repository.UpdateAsync(entity);
    }
}
