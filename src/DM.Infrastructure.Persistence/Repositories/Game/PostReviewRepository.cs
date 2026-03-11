using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Reviews;
using DM.Domain.Core.Reviews;
using Microsoft.EntityFrameworkCore;
using DbReview = DM.Infrastructure.Persistence.Entities.Shared.Review;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IPostReviewRepository" />
internal class PostReviewRepository : IPostReviewRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public PostReviewRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> CountAsync(Guid postId) => _dbContext.Reviews
        .TagWith("DM.PostReview.Count")
        .Where(r => r.TargetType == ReviewTargetType.Post &&
                    r.TargetId == postId &&
                    !r.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetAsync(Guid postId, PagingData paging) =>
        await _dbContext.Reviews
            .TagWith("DM.PostReview.GetByPost")
            .Where(r => r.TargetType == ReviewTargetType.Post &&
                        r.TargetId == postId &&
                        !r.IsRemoved)
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<Review?> GetAsync(Guid id) => _dbContext.Reviews
        .TagWith("DM.PostReview.GetById")
        .Where(r => !r.IsRemoved &&
                    r.ReviewId == id &&
                    r.TargetType == ReviewTargetType.Post)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<Review?> GetByAuthorAsync(Guid postId, Guid authorId) => _dbContext.Reviews
        .TagWith("DM.PostReview.GetByAuthor")
        .Where(r => r.TargetType == ReviewTargetType.Post &&
                    r.TargetId == postId &&
                    r.UserId == authorId &&
                    !r.IsRemoved)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountAllAsync(PostReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .TagWith("DM.PostReview.CountAll")
            .Where(r => r.TargetType == ReviewTargetType.Post && !r.IsRemoved);

        query = ApplyFilter(query, filter);

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetAllAsync(PagingData paging, PostReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .TagWith("DM.PostReview.GetAll")
            .Where(r => r.TargetType == ReviewTargetType.Post && !r.IsRemoved);

        query = ApplyFilter(query, filter);

        return await query
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid postId) => _dbContext.Reviews
        .TagWith("DM.PostReview.Exists")
        .AnyAsync(r => r.TargetType == ReviewTargetType.Post &&
                       r.UserId == authorId &&
                       r.TargetId == postId &&
                       !r.IsRemoved);

    /// <inheritdoc />
    public async Task<Review> CreateAsync(CreatePostReviewEntity entity)
    {
        var dbReview = new DbReview
        {
            ReviewId = entity.ReviewId,
            UserId = entity.UserId,
            TargetType = ReviewTargetType.Post,
            TargetId = entity.PostId,
            PostAuthorId = entity.PostAuthorId,
            GameId = entity.GameId,
            CreatedUtc = entity.CreatedUtc,
            SignValue = (short)entity.Sign,
            ReasonType = entity.ReasonType,
            IsApproved = true, // Post reviews are always approved
            IsRemoved = false
        };

        _dbContext.Reviews.Add(dbReview);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Reviews
            .TagWith("DM.PostReview.Created")
            .Where(r => r.ReviewId == dbReview.ReviewId)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<Review> UpdateAsync(UpdatePostReviewEntity entity)
    {
        var dbReview = await _dbContext.Reviews.FindAsync(entity.ReviewId);
        if (dbReview == null)
        {
            throw new InvalidOperationException($"Review {entity.ReviewId} not found");
        }

        if (entity.Sign.HasValue)
            dbReview.SignValue = (short)entity.Sign.Value;
        if (entity.ReasonType.HasValue)
            dbReview.ReasonType = entity.ReasonType.Value;
        if (entity.IsRemoved.HasValue)
            dbReview.IsRemoved = entity.IsRemoved.Value;
        if (entity.ModifiedUtc.HasValue)
            dbReview.ModifiedUtc = entity.ModifiedUtc.Value;
        if (entity.ModifiedByUserId.HasValue)
            dbReview.ModifiedByUserId = entity.ModifiedByUserId;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Reviews
            .TagWith("DM.PostReview.Updated")
            .Where(r => r.ReviewId == entity.ReviewId)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task UpdateUserQualityRatingAsync(Guid userId, int ratingDelta)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user != null)
        {
            user.QualityRating += ratingDelta;
            await _dbContext.SaveChangesAsync();
        }
    }

    // ═══ ELIGIBILITY ═══

    /// <inheritdoc />
    public async Task<PostInfo?> GetPostInfoAsync(Guid postId)
    {
        return await _dbContext.Posts
            .TagWith("DM.PostReview.GetPostInfo")
            .Include(p => p.Room)
            .Where(p => p.PostId == postId)
            .Select(p => new PostInfo
            {
                AuthorId = p.AuthorId,
                GameId = p.Room.GameId
            })
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public Task<bool> HasRecentReviewInGameAsync(Guid authorId, Guid gameId, DateTimeOffset cutoffDate) =>
        _dbContext.Reviews
            .TagWith("DM.PostReview.HasRecentInGame")
            .AnyAsync(r => r.TargetType == ReviewTargetType.Post &&
                           r.UserId == authorId &&
                           r.GameId == gameId &&
                           r.CreatedUtc >= cutoffDate &&
                           !r.IsRemoved);

    /// <inheritdoc />
    public Task<int> GetUserPostCountAsync(Guid userId) => _dbContext.Posts
        .TagWith("DM.PostReview.UserPostCount")
        .CountAsync(p => p.AuthorId == userId);

    // ═══ PRIVATE ═══

    private IQueryable<DbReview> ApplyFilter(IQueryable<DbReview> query, PostReviewFilter? filter)
    {
        if (filter == null)
            return query;

        if (filter.AuthorId.HasValue)
            query = query.Where(r => r.UserId == filter.AuthorId.Value);

        if (filter.RecipientId.HasValue)
            query = query.Where(r => r.PostAuthorId == filter.RecipientId.Value);

        if (filter.GameId.HasValue)
            query = query.Where(r => r.GameId == filter.GameId.Value);

        return query;
    }
}
