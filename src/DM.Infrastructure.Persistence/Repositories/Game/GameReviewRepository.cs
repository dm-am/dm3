using DM.Domain.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.GameReviews;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IGameReviewRepository" />
internal class GameReviewRepository : IGameReviewRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GameReviewRepository(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

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
    public Task<int> CountAllAsync(Guid viewerId, GameReviewFilter? filter = null)
    {
        var query = InGamesOpenTo(viewerId, _dbContext.GameReviews
            .TagWith("DM.GameReview.CountAll")
            .Where(r => !r.IsRemoved));

        query = ApplyFilter(query, filter);

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GameReview>> GetAllAsync(Guid viewerId, PagingData paging, GameReviewFilter? filter = null)
    {
        var query = InGamesOpenTo(viewerId, _dbContext.GameReviews
            .TagWith("DM.GameReview.GetAll")
            .Where(r => !r.IsRemoved));

        query = ApplyFilter(query, filter);

        return await ApplySort(query, filter)
            .Page(paging)
            .ProjectToGameReview()
            .ToArrayAsync();
    }

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
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
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
        if (entity.IsRemoved == true && !dbReview.IsRemoved)
            // Only while live: a second removal would overwrite the hand and clock of the first.
            SoftDelete.Mark(dbReview, entity.DeletedByUserId, entity.DeletedUtc ?? _dateTimeProvider.Now);
        else if (entity.IsRemoved == false)
            dbReview.IsRemoved = false;
        if (entity.ModifiedUtc.HasValue)
            dbReview.ModifiedUtc = entity.ModifiedUtc.Value;
        if (entity.ModifiedByUserId.HasValue)
            dbReview.ModifiedByUserId = entity.ModifiedByUserId;

        await _dbContext.SaveChangesAsync();

        // The update may be the removal, so the read-back lifts the soft-delete filter.
        return await _dbContext.GameReviews
            .IgnoreQueryFilters()
            .TagWith("DM.GameReview.Updated")
            .Where(r => r.GameReviewId == entity.ReviewId)
            .ProjectToGameReview()
            .FirstAsync();
    }

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

    /// <summary>
    /// The reviews whose game the reader may open, by the same filter every read
    /// of a game applies, so a game hidden from the reader names nothing here.
    /// </summary>
    private IQueryable<DbGameReview> InGamesOpenTo(Guid viewerId, IQueryable<DbGameReview> query)
    {
        var games = _dbContext.Games.Where(GameAccessibilityFilters.GameAvailable(viewerId));
        return query.Where(r => games.Any(g => g.GameId == r.GameId));
    }

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
            // The list lifts the account filter to keep the reviews of removed
            // accounts (RemovedAuthors), so a removed name is not searchable.
            var pattern = LikePatterns.Contains(filter.Search.Trim());
            query = query.Where(r =>
                EF.Functions.ILike(r.Text, pattern) ||
                (!r.Author.IsRemoved && EF.Functions.ILike(r.Author.Username, pattern)) ||
                EF.Functions.ILike(r.Game.Title, pattern));
        }

        return query;
    }

    /// <summary>
    /// List sorting by the client's sort options ("created", "author"); an option
    /// without a case here falls into the default order. For reviews WRITTEN by a user (AuthorId is fixed by the
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
            // A removed author sorts as an empty name, not by the name the removal took away.
            Expression<Func<DbGameReview, string>> name = filter?.AuthorId != null
                ? r => r.Game.Title
                : r => r.Author.IsRemoved ? "" : r.Author.Username;
            return query.OrderByDirection(name, !desc)
                .ThenByDirection(r => r.CreatedUtc, !desc)
                .ThenByDirection(r => r.GameReviewId, !desc);
        }

        return query.OrderByDirection(r => r.CreatedUtc, !desc).ThenByDirection(r => r.GameReviewId, !desc);
    }
}
