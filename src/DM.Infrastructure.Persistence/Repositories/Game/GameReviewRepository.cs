using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.GameReviews;
using Microsoft.EntityFrameworkCore;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;

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
    public Task<int> CountAsync(Guid gameId) => _dbContext.GameReviews
        .TagWith("DM.GameReview.Count")
        .Where(r => r.GameId == gameId && !r.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GameReview>> GetAsync(Guid gameId, PagingData paging) =>
        await _dbContext.GameReviews
            .TagWith("DM.GameReview.GetByGame")
            .Where(r => r.GameId == gameId && !r.IsRemoved)
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<GameReview>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<GameReview?> GetAsync(Guid id) => _dbContext.GameReviews
        .TagWith("DM.GameReview.GetById")
        .Where(r => !r.IsRemoved && r.GameReviewId == id)
        .ProjectTo<GameReview>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<GameReview?> GetByAuthorAsync(Guid gameId, Guid authorId) => _dbContext.GameReviews
        .TagWith("DM.GameReview.GetByAuthor")
        .Where(r => r.GameId == gameId &&
                    r.AuthorId == authorId &&
                    !r.IsRemoved)
        .ProjectTo<GameReview>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountAllAsync(GameReviewFilter? filter = null)
    {
        var query = _dbContext.GameReviews
            .TagWith("DM.GameReview.CountAll")
            .Where(r => !r.IsRemoved);

        query = ApplyFilter(query, filter);

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GameReview>> GetAllAsync(PagingData paging, GameReviewFilter? filter = null)
    {
        var query = _dbContext.GameReviews
            .TagWith("DM.GameReview.GetAll")
            .Where(r => !r.IsRemoved);

        query = ApplyFilter(query, filter);

        return await query
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<GameReview>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid gameId) => _dbContext.GameReviews
        .TagWith("DM.GameReview.Exists")
        .AnyAsync(r => r.AuthorId == authorId &&
                       r.GameId == gameId &&
                       !r.IsRemoved);

    /// <inheritdoc />
    public async Task<GameReview> CreateAsync(CreateGameReviewEntity entity)
    {
        var dbReview = new DbGameReview
        {
            GameReviewId = entity.ReviewId,
            AuthorId = entity.UserId,
            GameId = entity.GameId,
            CreatedUtc = entity.CreatedUtc,
            Text = entity.Text,
            IsRemoved = false
        };

        _dbContext.GameReviews.Add(dbReview);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.GameReviews
            .TagWith("DM.GameReview.Created")
            .Where(r => r.GameReviewId == dbReview.GameReviewId)
            .ProjectTo<GameReview>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<GameReview> UpdateAsync(UpdateGameReviewEntity entity)
    {
        var dbReview = await _dbContext.GameReviews.FindAsync(entity.ReviewId);
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

        return await _dbContext.GameReviews
            .TagWith("DM.GameReview.Updated")
            .Where(r => r.GameReviewId == entity.ReviewId)
            .ProjectTo<GameReview>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    // ═══ ELIGIBILITY ═══

    /// <inheritdoc />
    public async Task<bool> CanReviewGameAsync(Guid userId, Guid gameId)
    {
        // User can review a game if they have at least one post in it
        return await _dbContext.Posts
            .TagWith("DM.GameReview.CanReview")
            .AnyAsync(p => p.AuthorId == userId && p.Room.GameId == gameId);
    }

    /// <inheritdoc />
    public Task<int> GetUserPostCountAsync(Guid userId) => _dbContext.Posts
        .TagWith("DM.GameReview.UserPostCount")
        .CountAsync(p => p.AuthorId == userId);

    // ═══ PRIVATE ═══

    private IQueryable<DbGameReview> ApplyFilter(IQueryable<DbGameReview> query, GameReviewFilter? filter)
    {
        if (filter == null)
            return query;

        if (filter.AuthorId.HasValue)
            query = query.Where(r => r.AuthorId == filter.AuthorId.Value);

        if (filter.GameId.HasValue)
            query = query.Where(r => r.GameId == filter.GameId.Value);

        if (filter.GmId.HasValue)
        {
            query = query.Where(r => _dbContext.Games
                .Any(g => g.GameId == r.GameId && g.MasterId == filter.GmId.Value && !g.IsRemoved));
        }

        return query;
    }
}
