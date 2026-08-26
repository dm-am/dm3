using Npgsql;
using DM.Domain.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.PostReviews;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IPostReviewRepository" />
internal class PostReviewRepository : IPostReviewRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PostReviewRepository(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> CountAsync(Guid postId) => _dbContext.PostReviews
        .TagWith("DM.PostReview.Count")
        .Where(r => r.PostId == postId && !r.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<PostReview>> GetAsync(Guid postId, PagingData paging) =>
        await _dbContext.PostReviews
            .TagWith("DM.PostReview.GetByPost")
            .Where(r => r.PostId == postId && !r.IsRemoved)
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectToPostReview()
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<PostReview?> GetAsync(Guid id) => _dbContext.PostReviews
        .TagWith("DM.PostReview.GetById")
        .Where(r => !r.IsRemoved && r.PostReviewId == id)
        .ProjectToPostReview()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<PostReview?> GetByAuthorAsync(Guid postId, Guid authorId) => _dbContext.PostReviews
        .TagWith("DM.PostReview.GetByAuthor")
        .Where(r => r.PostId == postId &&
                    r.AuthorId == authorId &&
                    !r.IsRemoved)
        .ProjectToPostReview()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountAllAsync(PostReviewFilter? filter = null)
    {
        var query = _dbContext.PostReviews
            .TagWith("DM.PostReview.CountAll")
            .Where(r => !r.IsRemoved);

        query = ApplyFilter(query, filter);

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PostReview>> GetAllAsync(PagingData paging, PostReviewFilter? filter = null)
    {
        var query = _dbContext.PostReviews
            .TagWith("DM.PostReview.GetAll")
            .Where(r => !r.IsRemoved);

        query = ApplyFilter(query, filter);

        return await query
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectToPostReview()
            .ToArrayAsync();
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid postId) => _dbContext.PostReviews
        .TagWith("DM.PostReview.Exists")
        .AnyAsync(r => r.AuthorId == authorId &&
                       r.PostId == postId &&
                       !r.IsRemoved);

    /// <inheritdoc />
    /// <remarks>
    /// The row and the post author's counter in one transaction. QualityRating is
    /// a stored column and not a sum over the reviews, so a review written with
    /// the counter left behind — or a counter moved for a review the unique index
    /// then refused — is a drift nothing recomputes: the number on the profile and
    /// the rows it counts have no way left to be reconciled. The strategy wrapper
    /// is required because the API host configures EnableRetryOnFailure.
    /// </remarks>
    public async Task<PostReview> CreateAsync(CreatePostReviewEntity entity, int qualityRatingDelta)
    {
        var dbReview = new DbPostReview
        {
            PostReviewId = entity.PostReviewId,
            AuthorId = entity.AuthorId,
            PostId = entity.PostId,
            PostAuthorId = entity.PostAuthorId,
            GameId = entity.GameId,
            CreatedUtc = entity.CreatedUtc,
            Sign = entity.Sign,
            Text = entity.Text,
            IsRemoved = false
        };

        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            await MoveQualityRating(entity.PostAuthorId, qualityRatingDelta);

            _dbContext.PostReviews.Add(dbReview);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                // The caller pre-checks for an existing post review; this is the race
                // where two concurrent requests both pass that check. Translated here
                // so the domain does not have to know the storage engine's error codes.
                // The transaction is not committed, so the counter this attempt moved
                // goes back with it.
                _dbContext.ChangeTracker.Clear();
                throw new DuplicateEntityException("Duplicate post review", ex);
            }

            await transaction.CommitAsync();
        });

        return await _dbContext.PostReviews
            .TagWith("DM.PostReview.Created")
            .Where(r => r.PostReviewId == dbReview.PostReviewId)
            .ProjectToPostReview()
            .FirstAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The same transaction as Create, for the same reason. Both updates this
    /// method serves owe the post author's counter something — an edit the
    /// difference between the two signs, a removal the sign taken back — and the
    /// counter used to be moved by a call of its own that committed before the row
    /// was written at all. A refusal in the gap left the profile carrying a sign
    /// the review does not have, with nothing to recompute it from.
    /// </remarks>
    public async Task<PostReview> UpdateAsync(UpdatePostReviewEntity entity, int qualityRatingDelta)
    {
        // The strategy wrapper is required because the API host configures
        // EnableRetryOnFailure.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            // Read inside the block: the clear above drops the tracked review, so
            // it has to be loaded again. On the first attempt this costs nothing.
            var dbReview = await _dbContext.PostReviews.FindAsync(entity.PostReviewId);
            if (dbReview == null)
            {
                throw new InvalidOperationException($"Review {entity.PostReviewId} not found");
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // The row carries whose counter this is: the same denormalised column
            // the reads filter by, rather than a second copy passed in alongside.
            await MoveQualityRating(dbReview.PostAuthorId, qualityRatingDelta);

            if (entity.Sign.HasValue)
                dbReview.Sign = entity.Sign.Value;
            if (entity.Text != null)
                dbReview.Text = entity.Text;
            if (entity.IsRemoved == true)
                // Through the one call that writes the flag and the audit together,
                // as the endorsement removal does: a review a senior moderator took
                // down with no hand recorded cannot be reviewed afterwards.
                SoftDelete.Mark(dbReview, entity.DeletedByUserId, entity.DeletedUtc ?? _dateTimeProvider.Now);
            else if (entity.IsRemoved == false)
                dbReview.IsRemoved = false;
            if (entity.ModifiedUtc.HasValue)
                dbReview.ModifiedUtc = entity.ModifiedUtc.Value;
            if (entity.ModifiedByUserId.HasValue)
                dbReview.ModifiedByUserId = entity.ModifiedByUserId;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        });

        // IgnoreQueryFilters, because one of the updates this method serves is
        // the removal: the global filter drops soft-deleted rows, so reading the
        // row back through it throws "Sequence contains no elements" on the way
        // out of every delete. The caller asked for this row by its identifier
        // and has just written it, so the filter has nothing to protect here.
        return await _dbContext.PostReviews
            .IgnoreQueryFilters()
            .TagWith("DM.PostReview.Updated")
            .Where(r => r.PostReviewId == entity.PostReviewId)
            .ProjectToPostReview()
            .FirstAsync();
    }

    /// <summary>
    /// Move the post author's stored quality rating by what a review is worth.
    /// </summary>
    /// <remarks>
    /// Private, and called only from inside the two transactions above: this is
    /// the write that must not happen on its own.
    ///
    /// Set-based rather than read-modify-write, so two raters moving the same
    /// author's counter at once do not overwrite one another with the value each
    /// of them read. IgnoreQueryFilters, because the row it has to reach may
    /// belong to a deactivated account — their reviews are still editable and
    /// removable by moderation, and under the global filter the update would match
    /// nothing, report success and leave the counter carrying a sign that is gone.
    /// </remarks>
    private async Task MoveQualityRating(Guid postAuthorId, int delta)
    {
        if (delta == 0)
        {
            return;
        }

        await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.UserId == postAuthorId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.QualityRating, x => x.QualityRating + delta));
    }

    // ═══ ELIGIBILITY ═══

    /// <inheritdoc />
    /// <remarks>
    /// Reached through the rooms, like every other read of a post, so the rater's
    /// scope is applied by the one filter that expresses it. Asking Posts directly
    /// answered for any post whose identifier the caller happened to know: a post
    /// in a private room could be rated by somebody with no way to read it, and
    /// the rating went on to move its author's quality rating all the same.
    /// </remarks>
    public async Task<PostInfo?> GetPostInfoAsync(Guid postId, Guid userId)
    {
        return await _dbContext.Rooms
            .TagWith("DM.PostReview.GetPostInfo")
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .SelectMany(r => r.Posts)
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
        _dbContext.PostReviews
            .TagWith("DM.PostReview.HasRecentInGame")
            .AnyAsync(r => r.AuthorId == authorId &&
                           r.GameId == gameId &&
                           r.CreatedUtc >= cutoffDate &&
                           !r.IsRemoved);

    /// <inheritdoc />
    /// <remarks>
    /// The denormalised counter, the same one the newbie badge is computed from;
    /// a COUNT over posts is a second number and drifts from it on every removal.
    /// </remarks>
    public Task<int> GetUserPostCountAsync(Guid userId) => _dbContext.Users
        .TagWith("DM.PostReview.UserPostCount")
        .Where(u => u.UserId == userId)
        .Select(u => u.QuantityRating)
        .FirstOrDefaultAsync();

    // ═══ PRIVATE ═══

    private IQueryable<DbPostReview> ApplyFilter(IQueryable<DbPostReview> query, PostReviewFilter? filter)
    {
        if (filter == null)
            return query;

        if (filter.AuthorId.HasValue)
            query = query.Where(r => r.AuthorId == filter.AuthorId.Value);

        if (filter.RecipientId.HasValue)
            query = query.Where(r => r.PostAuthorId == filter.RecipientId.Value);

        if (filter.GameId.HasValue)
            query = query.Where(r => r.GameId == filter.GameId.Value);

        return query;
    }
}
