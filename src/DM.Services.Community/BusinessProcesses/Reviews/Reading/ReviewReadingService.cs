using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;

namespace DM.Services.Community.BusinessProcesses.Reviews.Reading;

/// <inheritdoc />
internal class ReviewReadingService : IReviewReadingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IReviewReadingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public ReviewReadingService(
        IIntentionManager intentionManager,
        IReviewReadingRepository repository,
        IIdentityProvider identityProvider)
    {
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> reviews, PagingResult paging)> Get(PagingQuery query, bool onlyApproved)
    {
        onlyApproved = onlyApproved || !_intentionManager.IsAllowed(ReviewIntention.ReadUnapproved);
        var totalCount = await _repository.Count(onlyApproved);
        var pagingData = new PagingData(query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);

        var reviews = await _repository.Get(pagingData, onlyApproved);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Review> Get(Guid id)
    {
        var review = await _repository.Get(id);
        if (review == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Review not found");
        }

        // Platform reviews require approval check
        if (review.TargetType == ReviewTargetType.Platform &&
            !review.Approved &&
            !_intentionManager.IsAllowed(ReviewIntention.ReadUnapproved))
        {
            throw new HttpException(HttpStatusCode.Gone, "Review not found");
        }

        return review;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> reviews, PagingResult paging)> GetUserReviews(
        Guid targetUserId, PagingQuery query)
    {
        var totalCount = await _repository.CountUserReviews(targetUserId);
        var pagingData = new PagingData(query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);

        var reviews = await _repository.GetUserReviews(targetUserId, pagingData);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> reviews, PagingResult paging)> GetGameReviews(
        Guid gameId, PagingQuery query)
    {
        var totalCount = await _repository.CountGameReviews(gameId);
        var pagingData = new PagingData(query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);

        var reviews = await _repository.GetGameReviews(gameId, pagingData);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public Task<Review?> GetUserReviewByAuthor(Guid targetUserId, Guid authorId) =>
        _repository.GetUserReviewByAuthor(targetUserId, authorId);

    /// <inheritdoc />
    public Task<Review?> GetGameReviewByAuthor(Guid gameId, Guid authorId) =>
        _repository.GetGameReviewByAuthor(gameId, authorId);

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> reviews, PagingResult paging)> GetAllGameReviews(
        PagingQuery query, GameReviewFilter? filter = null)
    {
        var totalCount = await _repository.CountAllGameReviews(filter);
        var pagingData = new PagingData(query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);

        var reviews = await _repository.GetAllGameReviews(pagingData, filter);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> reviews, PagingResult paging)> GetAllUserReviews(
        PagingQuery query, UserReviewFilter? filter = null)
    {
        var totalCount = await _repository.CountAllUserReviews(filter);
        var pagingData = new PagingData(query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);

        var reviews = await _repository.GetAllUserReviews(pagingData, filter);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> reviews, PagingResult paging)> GetPostReviews(
        Guid postId, PagingQuery query)
    {
        var totalCount = await _repository.CountPostReviews(postId);
        var pagingData = new PagingData(query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);

        var reviews = await _repository.GetPostReviews(postId, pagingData);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public Task<Review?> GetPostReviewByAuthor(Guid postId, Guid authorId) =>
        _repository.GetPostReviewByAuthor(postId, authorId);

    /// <inheritdoc />
    public async Task<(IEnumerable<Review> reviews, PagingResult paging)> GetAllPostReviews(
        PagingQuery query, PostReviewFilter? filter = null)
    {
        var totalCount = await _repository.CountAllPostReviews(filter);
        var pagingData = new PagingData(query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);

        var reviews = await _repository.GetAllPostReviews(pagingData, filter);
        return (reviews, pagingData.Result);
    }
}