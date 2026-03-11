using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Community.Features.PlatformReviews;
using DM.Domain.Core.Reviews;
using Microsoft.EntityFrameworkCore;
using DbReview = DM.Infrastructure.Persistence.Entities.Shared.Review;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class PlatformReviewRepository : IPlatformReviewRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public PlatformReviewRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<int> CountAsync(bool approvedOnly) => _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.Platform &&
                    (!approvedOnly || r.IsApproved) &&
                    !r.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetAsync(PagingData paging, bool approvedOnly) => await _dbContext.Reviews
        .Where(r => r.TargetType == ReviewTargetType.Platform &&
                    (!approvedOnly || r.IsApproved) &&
                    !r.IsRemoved)
        .OrderByDescending(r => r.CreatedUtc)
        .Page(paging)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .ToArrayAsync();

    /// <inheritdoc />
    public Task<Review?> GetAsync(Guid id) => _dbContext.Reviews
        .Where(r => !r.IsRemoved &&
                    r.ReviewId == id &&
                    r.TargetType == ReviewTargetType.Platform)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<bool> UserHasReviewAsync(Guid userId) => _dbContext.Reviews
        .AnyAsync(r => r.UserId == userId &&
                       r.TargetType == ReviewTargetType.Platform &&
                       !r.IsRemoved);

    /// <inheritdoc />
    public async Task<Review> CreateAsync(CreatePlatformReviewEntity entity)
    {
        var dbReview = new DbReview
        {
            ReviewId = entity.ReviewId,
            UserId = entity.UserId,
            TargetType = ReviewTargetType.Platform,
            TargetId = null,
            CreatedUtc = entity.CreatedUtc,
            Text = entity.Text,
            IsApproved = entity.IsApproved,
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
    public async Task<Review> UpdateAsync(UpdatePlatformReviewEntity entity)
    {
        var dbReview = await _dbContext.Reviews.FindAsync(entity.ReviewId);
        if (dbReview == null)
        {
            throw new InvalidOperationException($"Review {entity.ReviewId} not found");
        }

        if (entity.Text != null)
            dbReview.Text = entity.Text;
        if (entity.IsApproved.HasValue)
            dbReview.IsApproved = entity.IsApproved.Value;
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
}
