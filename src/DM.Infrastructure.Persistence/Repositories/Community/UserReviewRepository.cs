using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Community.Features.UserReviews;
using DM.Domain.Core.Reviews;
using Microsoft.EntityFrameworkCore;
using DbReview = DM.Infrastructure.Persistence.Entities.Shared.Review;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class UserReviewRepository : IUserReviewRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public UserReviewRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> CountAsync(Guid targetUserId) => _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.User &&
                    r.TargetId == targetUserId &&
                    !r.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetAsync(Guid targetUserId, PagingData paging) =>
        await _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.User &&
                        r.TargetId == targetUserId &&
                        !r.IsRemoved)
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<Review?> GetAsync(Guid id) => _dbContext.Reviews
        .Where(r => !r.IsRemoved &&
                    r.ReviewId == id &&
                    r.TargetType == ReviewTargetType.User)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<Review?> GetByAuthorAsync(Guid targetUserId, Guid authorId) => _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.User &&
                    r.TargetId == targetUserId &&
                    r.UserId == authorId &&
                    !r.IsRemoved)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountAllAsync(UserReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.User && !r.IsRemoved);

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
    public async Task<IEnumerable<Review>> GetAllAsync(PagingData paging, UserReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.User && !r.IsRemoved);

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

    // ═══ WRITE ═══

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid targetUserId) => _dbContext.Reviews
        .AnyAsync(r => r.TargetType == ReviewTargetType.User &&
                       r.UserId == authorId &&
                       r.TargetId == targetUserId &&
                       !r.IsRemoved);

    /// <inheritdoc />
    public async Task<Review> CreateAsync(CreateUserReviewEntity entity)
    {
        var dbReview = new DbReview
        {
            ReviewId = entity.ReviewId,
            UserId = entity.UserId,
            TargetType = ReviewTargetType.User,
            TargetId = entity.TargetUserId,
            CreatedUtc = entity.CreatedUtc,
            Text = entity.Text,
            IsApproved = true, // User reviews are always approved
            IsRemoved = false
        };

        _dbContext.Reviews.Add(dbReview);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Reviews
            .Where(r => r.ReviewId == dbReview.ReviewId)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<Review> UpdateAsync(UpdateUserReviewEntity entity)
    {
        var dbReview = await _dbContext.Reviews.FindAsync(entity.ReviewId);
        if (dbReview == null)
        {
            throw new InvalidOperationException($"Review {entity.ReviewId} not found");
        }

        if (entity.Text != null)
            dbReview.Text = entity.Text;
        if (entity.IsRemoved.HasValue)
            dbReview.IsRemoved = entity.IsRemoved.Value;
        if (entity.ModifiedUtc.HasValue)
            dbReview.ModifiedUtc = entity.ModifiedUtc.Value;
        if (entity.ModifiedByUserId.HasValue)
            dbReview.ModifiedByUserId = entity.ModifiedByUserId;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Reviews
            .Where(r => r.ReviewId == entity.ReviewId)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    // ═══ ELIGIBILITY ═══

    /// <inheritdoc />
    public async Task<bool> HavePlayedTogetherAsync(Guid userId1, Guid userId2)
    {
        // Check if both users have posts in the same game
        var user1Games = _dbContext.Posts
            .Where(p => p.AuthorId == userId1)
            .Select(p => p.Room.GameId)
            .Distinct();

        var user2Games = _dbContext.Posts
            .Where(p => p.AuthorId == userId2)
            .Select(p => p.Room.GameId)
            .Distinct();

        return await user1Games.Intersect(user2Games).AnyAsync();
    }

    /// <inheritdoc />
    public Task<int> GetUserPostCountAsync(Guid userId) => _dbContext.Posts
        .CountAsync(p => p.AuthorId == userId);
}
