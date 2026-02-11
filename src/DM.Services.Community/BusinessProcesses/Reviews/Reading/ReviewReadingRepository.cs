using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Reviews.Reading;

/// <inheritdoc />
internal class ReviewReadingRepository : IReviewReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ReviewReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<int> Count(bool approvedOnly) => _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.Platform &&
                    (!approvedOnly || r.IsApproved) &&
                    !r.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> Get(PagingData paging, bool approvedOnly) => await _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.Platform &&
                    (!approvedOnly || r.IsApproved) &&
                    !r.IsRemoved)
        .OrderByDescending(r => r.CreatedUtc)
        .Page(paging)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .ToArrayAsync();

    /// <inheritdoc />
    public Task<Review?> Get(Guid id) => _dbContext.Reviews
        .Where(r => !r.IsRemoved && r.ReviewId == id)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountUserReviews(Guid targetUserId) => _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.User &&
                    r.TargetId == targetUserId &&
                    !r.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetUserReviews(Guid targetUserId, PagingData paging) =>
        await _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.User &&
                        r.TargetId == targetUserId &&
                        !r.IsRemoved)
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<int> CountGameReviews(Guid gameId) => _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.Game &&
                    r.TargetId == gameId &&
                    !r.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetGameReviews(Guid gameId, PagingData paging) =>
        await _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.Game &&
                        r.TargetId == gameId &&
                        !r.IsRemoved)
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<Review?> GetUserReviewByAuthor(Guid targetUserId, Guid authorId) => _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.User &&
                    r.TargetId == targetUserId &&
                    r.UserId == authorId &&
                    !r.IsRemoved)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<Review?> GetGameReviewByAuthor(Guid gameId, Guid authorId) => _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.Game &&
                    r.TargetId == gameId &&
                    r.UserId == authorId &&
                    !r.IsRemoved)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountAllGameReviews(GameReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.Game);

        if (filter != null)
        {
            if (filter.AuthorId.HasValue)
                query = query.Where(r => r.UserId == filter.AuthorId.Value);
            if (filter.GameId.HasValue)
                query = query.Where(r => r.TargetId == filter.GameId.Value);
            if (filter.GmId.HasValue)
            {
                query = query.Where(r => _dbContext.Games
                    .Any(g => g.GameId == r.TargetId && g.MasterId == filter.GmId.Value && !g.IsRemoved));
            }
        }

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetAllGameReviews(PagingData paging, GameReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.Game);

        if (filter != null)
        {
            if (filter.AuthorId.HasValue)
                query = query.Where(r => r.UserId == filter.AuthorId.Value);
            if (filter.GameId.HasValue)
                query = query.Where(r => r.TargetId == filter.GameId.Value);
            if (filter.GmId.HasValue)
            {
                query = query.Where(r => _dbContext.Games
                    .Any(g => g.GameId == r.TargetId && g.MasterId == filter.GmId.Value && !g.IsRemoved));
            }
        }

        return await query
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public Task<int> CountAllUserReviews(UserReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.User);

        if (filter != null)
        {
            if (filter.AuthorId.HasValue)
                query = query.Where(r => r.UserId == filter.AuthorId.Value);
            if (filter.RecipientId.HasValue)
                query = query.Where(r => r.TargetId == filter.RecipientId.Value);
        }

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetAllUserReviews(PagingData paging, UserReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.User);

        if (filter != null)
        {
            if (filter.AuthorId.HasValue)
                query = query.Where(r => r.UserId == filter.AuthorId.Value);
            if (filter.RecipientId.HasValue)
                query = query.Where(r => r.TargetId == filter.RecipientId.Value);
        }

        return await query
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public Task<int> CountPostReviews(Guid postId) => _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.Post &&
                    r.TargetId == postId)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetPostReviews(Guid postId, PagingData paging) =>
        await _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.Post &&
                        r.TargetId == postId)
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<Review?> GetPostReviewByAuthor(Guid postId, Guid authorId) => _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.Post &&
                    r.TargetId == postId &&
                    r.UserId == authorId)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountAllPostReviews(PostReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.Post);

        if (filter != null)
        {
            if (filter.AuthorId.HasValue)
                query = query.Where(r => r.UserId == filter.AuthorId.Value);
            if (filter.RecipientId.HasValue)
                query = query.Where(r => r.PostAuthorId == filter.RecipientId.Value);
            if (filter.GameId.HasValue)
                query = query.Where(r => r.GameId == filter.GameId.Value);
        }

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetAllPostReviews(PagingData paging, PostReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.Post);

        if (filter != null)
        {
            if (filter.AuthorId.HasValue)
                query = query.Where(r => r.UserId == filter.AuthorId.Value);
            if (filter.RecipientId.HasValue)
                query = query.Where(r => r.PostAuthorId == filter.RecipientId.Value);
            if (filter.GameId.HasValue)
                query = query.Where(r => r.GameId == filter.GameId.Value);
        }

        return await query
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }
}