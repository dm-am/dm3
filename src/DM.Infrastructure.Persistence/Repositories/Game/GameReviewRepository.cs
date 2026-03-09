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
using DbReview = DM.Infrastructure.Persistence.Entities.CrossDomain.Review;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IGameReviewRepository" />
internal class GameReviewRepository : IGameReviewRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public GameReviewRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> CountAsync(Guid gameId) => _dbContext.Reviews
        .TagWith("DM.GameReview.Count")
        .Where(r => r.TargetType == ReviewTargetType.Game &&
                    r.TargetId == gameId &&
                    !r.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetAsync(Guid gameId, PagingData paging) =>
        await _dbContext.Reviews
            .TagWith("DM.GameReview.GetByGame")
            .Where(r => r.TargetType == ReviewTargetType.Game &&
                        r.TargetId == gameId &&
                        !r.IsRemoved)
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<Review?> GetAsync(Guid id) => _dbContext.Reviews
        .TagWith("DM.GameReview.GetById")
        .Where(r => !r.IsRemoved &&
                    r.ReviewId == id &&
                    r.TargetType == ReviewTargetType.Game)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<Review?> GetByAuthorAsync(Guid gameId, Guid authorId) => _dbContext.Reviews
        .TagWith("DM.GameReview.GetByAuthor")
        .Where(r => r.TargetType == ReviewTargetType.Game &&
                    r.TargetId == gameId &&
                    r.UserId == authorId &&
                    !r.IsRemoved)
        .ProjectTo<Review>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountAllAsync(GameReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .TagWith("DM.GameReview.CountAll")
            .Where(r => r.TargetType == ReviewTargetType.Game && !r.IsRemoved);

        query = ApplyFilter(query, filter);

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Review>> GetAllAsync(PagingData paging, GameReviewFilter? filter = null)
    {
        var query = _dbContext.Reviews
            .TagWith("DM.GameReview.GetAll")
            .Where(r => r.TargetType == ReviewTargetType.Game && !r.IsRemoved);

        query = ApplyFilter(query, filter);

        return await query
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid gameId) => _dbContext.Reviews
        .TagWith("DM.GameReview.Exists")
        .AnyAsync(r => r.TargetType == ReviewTargetType.Game &&
                       r.UserId == authorId &&
                       r.TargetId == gameId &&
                       !r.IsRemoved);

    /// <inheritdoc />
    public async Task<Review> CreateAsync(CreateGameReviewEntity entity)
    {
        var dbReview = new DbReview
        {
            ReviewId = entity.ReviewId,
            UserId = entity.UserId,
            TargetType = ReviewTargetType.Game,
            TargetId = entity.GameId,
            CreatedUtc = entity.CreatedUtc,
            Text = entity.Text,
            IsApproved = true, // Game reviews are always approved
            IsRemoved = false
        };

        _dbContext.Reviews.Add(dbReview);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Reviews
            .TagWith("DM.GameReview.Created")
            .Where(r => r.ReviewId == dbReview.ReviewId)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<Review> UpdateAsync(UpdateGameReviewEntity entity)
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
            .TagWith("DM.GameReview.Updated")
            .Where(r => r.ReviewId == entity.ReviewId)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    // ═══ ELIGIBILITY ═══

    /// <inheritdoc />
    public async Task<bool> CanReviewGameAsync(Guid userId, Guid gameId)
    {
        // User can review a game if they have at least one post in it
        return await _dbContext.Posts
            .TagWith("DM.GameReview.CanReview")
            .Include(p => p.Room)
            .AnyAsync(p => p.AuthorId == userId && p.Room.GameId == gameId);
    }

    /// <inheritdoc />
    public Task<int> GetUserPostCountAsync(Guid userId) => _dbContext.Posts
        .TagWith("DM.GameReview.UserPostCount")
        .CountAsync(p => p.AuthorId == userId);

    // ═══ PRIVATE ═══

    private IQueryable<DbReview> ApplyFilter(IQueryable<DbReview> query, GameReviewFilter? filter)
    {
        if (filter == null)
            return query;

        if (filter.AuthorId.HasValue)
            query = query.Where(r => r.UserId == filter.AuthorId.Value);

        if (filter.GameId.HasValue)
            query = query.Where(r => r.TargetId == filter.GameId.Value);

        if (filter.GmId.HasValue)
        {
            query = query.Where(r => _dbContext.Games
                .Any(g => g.GameId == r.TargetId && g.AuthorId == filter.GmId.Value && !g.IsRemoved));
        }

        return query;
    }
}
