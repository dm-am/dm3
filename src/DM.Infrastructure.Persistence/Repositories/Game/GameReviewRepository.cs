using Npgsql;
using DM.Domain.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.GameReviews;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IGameReviewRepository" />
internal class GameReviewRepository : IGameReviewRepository
{
    private readonly DmDbContext _dbContext;

    public GameReviewRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
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
            .ProjectToGameReview()
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<GameReview?> GetAsync(Guid id) => _dbContext.GameReviews
        .TagWith("DM.GameReview.GetById")
        .Where(r => !r.IsRemoved && r.GameReviewId == id)
        .ProjectToGameReview()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<GameReview?> GetByAuthorAsync(Guid gameId, Guid authorId) => _dbContext.GameReviews
        .TagWith("DM.GameReview.GetByAuthor")
        .Where(r => r.GameId == gameId &&
                    r.AuthorId == authorId &&
                    !r.IsRemoved)
        .ProjectToGameReview()
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

        return await ApplySort(query, filter)
            .Page(paging)
            .ProjectToGameReview()
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
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // The caller pre-checks for an existing game review; this is the race
            // where two concurrent requests both pass that check. Translated here
            // so the domain does not have to know the storage engine's error codes.
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateEntityException("Duplicate game review", ex);
        }

        return await _dbContext.GameReviews
            .TagWith("DM.GameReview.Created")
            .Where(r => r.GameReviewId == dbReview.GameReviewId)
            .ProjectToGameReview()
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
            .ProjectToGameReview()
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
    /// <remarks>
    /// The denormalised counter, not a COUNT over posts: posts are counted under
    /// the !IsRemoved filter while the counter is decremented on removal, so the
    /// two are different numbers, and the badge on the profile is computed from
    /// the counter. Counting here and reading the counter for post reviews and
    /// endorsements let one user be a newbie for one of the three and not for the
    /// others.
    /// </remarks>
    public Task<int> GetUserPostCountAsync(Guid userId) => _dbContext.Users
        .TagWith("DM.GameReview.UserPostCount")
        .Where(u => u.UserId == userId)
        .Select(u => u.QuantityRating)
        .FirstOrDefaultAsync();

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

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // ILIKE over the text, the author's name and the game's title at
            // once — three SQL conditions joined by OR, the shape the
            // endorsement list already uses. This is "find where X is
            // mentioned", not three separate search buckets.
            var pattern = LikePatterns.Contains(filter.Search.Trim());
            query = query.Where(r =>
                EF.Functions.ILike(r.Text, pattern) ||
                EF.Functions.ILike(r.Author.Username, pattern) ||
                EF.Functions.ILike(r.Game.Title, pattern));
        }

        return query;
    }

    /// <summary>
    /// List sorting. Supported fields are kept in step with the FE sort
    /// options ("created", "author") — a new option on the client without a
    /// matching case here silently falls into the default order, so the SSOT
    /// is this switch. For reviews WRITTEN by a user (AuthorId is fixed by the
    /// filter) "author" means the other side of the pair, and that is the
    /// game's title. Secondary keys make the order deterministic when primary
    /// values are equal.
    /// </summary>
    private static IOrderedQueryable<DbGameReview> ApplySort(
        IQueryable<DbGameReview> query, GameReviewFilter? filter)
    {
        var sortBy = (filter?.SortBy ?? "created").ToLowerInvariant();
        var desc = string.IsNullOrEmpty(filter?.SortOrder) ||
                   filter.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

        if (sortBy == "author")
        {
            var byGame = filter?.AuthorId != null;
            var ordered = (byGame, desc) switch
            {
                (true, true) => query.OrderByDescending(r => r.Game.Title),
                (true, false) => query.OrderBy(r => r.Game.Title),
                (false, true) => query.OrderByDescending(r => r.Author.Username),
                (false, false) => query.OrderBy(r => r.Author.Username),
            };
            return desc
                ? ordered.ThenByDescending(r => r.CreatedUtc).ThenByDescending(r => r.GameReviewId)
                : ordered.ThenBy(r => r.CreatedUtc).ThenBy(r => r.GameReviewId);
        }

        return desc
            ? query.OrderByDescending(r => r.CreatedUtc).ThenByDescending(r => r.GameReviewId)
            : query.OrderBy(r => r.CreatedUtc).ThenBy(r => r.GameReviewId);
    }
}
