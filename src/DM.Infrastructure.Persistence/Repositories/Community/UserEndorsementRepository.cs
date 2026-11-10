using DM.Domain.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using DbUserEndorsement = DM.Infrastructure.Persistence.Entities.Community.UserEndorsement;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class UserEndorsementRepository : IUserEndorsementRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserEndorsementRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<UserEndorsement?> GetAsync(Guid id) => _dbContext.UserEndorsements
        .Where(e => !e.IsRemoved && e.UserEndorsementId == id)
        .ProjectToUserEndorsement()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountAllAsync(UserEndorsementFilter? filter = null) =>
        ApplyFilter(_dbContext.UserEndorsements.Where(e => !e.IsRemoved), filter)
            .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<UserEndorsement>> GetAllAsync(PagingData paging, UserEndorsementFilter? filter = null)
    {
        var query = ApplyFilter(_dbContext.UserEndorsements.Where(e => !e.IsRemoved), filter);

        var sorted = ApplySort(query, filter);

        return await sorted
            .Page(paging)
            .ProjectToUserEndorsement()
            .ToArrayAsync();
    }

    private static IQueryable<DbUserEndorsement> ApplyFilter(
        IQueryable<DbUserEndorsement> query, UserEndorsementFilter? filter)
    {
        if (filter == null) return query;

        if (filter.AuthorId.HasValue)
            query = query.Where(e => e.AuthorId == filter.AuthorId.Value);
        if (filter.RecipientId.HasValue)
            query = query.Where(e => e.TargetUserId == filter.RecipientId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = LikePatterns.Contains(filter.Search.Trim());
            query = query.Where(e =>
                EF.Functions.ILike(e.Text, pattern) ||
                EF.Functions.ILike(e.Author.Username, pattern) ||
                EF.Functions.ILike(e.TargetUser.Username, pattern));
        }

        return query;
    }

    /// <summary>
    /// List sorting by the client's ReviewsFilter SORT_OPTIONS ("created", "author");
    /// an option without a case here falls into the default order.
    /// For "given" endorsements (AuthorId is fixed by the filter)
    /// "author" means the other side of the pair — we sort by the
    /// recipient's name. Secondary keys (CreatedUtc, then Id) make
    /// the order deterministic when primary values are equal.
    /// </summary>
    private static IOrderedQueryable<DbUserEndorsement> ApplySort(
        IQueryable<DbUserEndorsement> query, UserEndorsementFilter? filter)
    {
        var sortBy = (filter?.SortBy ?? "created").ToLowerInvariant();
        var desc = string.IsNullOrEmpty(filter?.SortOrder) ||
                   filter.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

        if (sortBy == "author")
        {
            Expression<Func<DbUserEndorsement, string>> name = filter?.AuthorId != null
                ? e => e.TargetUser.Username
                : e => e.Author.Username;
            return query.OrderByDirection(name, !desc)
                .ThenByDirection(e => e.CreatedUtc, !desc)
                .ThenByDirection(e => e.UserEndorsementId, !desc);
        }

        return query.OrderByDirection(e => e.CreatedUtc, !desc).ThenByDirection(e => e.UserEndorsementId, !desc);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid targetUserId) => _dbContext.UserEndorsements
        .AnyAsync(e => e.AuthorId == authorId &&
                       e.TargetUserId == targetUserId &&
                       !e.IsRemoved);

    /// <inheritdoc />
    public async Task<UserEndorsement> CreateAsync(CreateUserEndorsementEntity entity)
    {
        var dbEndorsement = new DbUserEndorsement
        {
            UserEndorsementId = entity.EndorsementId,
            AuthorId = entity.UserId,
            TargetUserId = entity.TargetUserId,
            CreatedUtc = entity.CreatedUtc,
            Text = entity.Text,
            IsRemoved = false
        };

        _dbContext.UserEndorsements.Add(dbEndorsement);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
        {
            // The caller pre-checks for an existing endorsement; this is the race
            // where two concurrent requests both pass that check. Translated here
            // so the domain does not have to know the storage engine's error codes.
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateEntityException("Duplicate endorsement", ex);
        }

        return await _dbContext.UserEndorsements
            .Where(e => e.UserEndorsementId == dbEndorsement.UserEndorsementId)
            .ProjectToUserEndorsement()
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<UserEndorsement> UpdateAsync(UpdateUserEndorsementEntity entity)
    {
        var dbEndorsement = await _dbContext.UserEndorsements.FindAsync(entity.EndorsementId);
        if (dbEndorsement == null)
        {
            throw new InvalidOperationException($"Endorsement {entity.EndorsementId} not found");
        }

        if (entity.Text != null)
            dbEndorsement.Text = entity.Text;
        if (entity.IsRemoved == true)
            SoftDelete.Mark(dbEndorsement, entity.DeletedByUserId, entity.DeletedUtc ?? _dateTimeProvider.Now);
        else if (entity.IsRemoved == false)
            dbEndorsement.IsRemoved = false;
        if (entity.ModifiedUtc.HasValue)
            dbEndorsement.ModifiedUtc = entity.ModifiedUtc.Value;
        if (entity.ModifiedByUserId.HasValue)
            dbEndorsement.ModifiedByUserId = entity.ModifiedByUserId;

        await _dbContext.SaveChangesAsync();

        // The update may be the removal, so the read-back lifts the soft-delete filter.
        return await _dbContext.UserEndorsements
            .IgnoreQueryFilters()
            .Where(e => e.UserEndorsementId == entity.EndorsementId)
            .ProjectToUserEndorsement()
            .FirstAsync();
    }

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
    /// <remarks>
    /// The denormalised counter, not a COUNT over posts: posts are counted under
    /// the !IsRemoved filter while the counter is decremented on removal, so the
    /// two are different numbers and reading each in a different place made the
    /// newbie badge and the right to write disagree about one user.
    /// </remarks>
    public Task<int> GetUserPostCountAsync(Guid userId) => _dbContext.Users
        .Where(u => u.UserId == userId)
        .Select(u => u.QuantityRating)
        .FirstOrDefaultAsync();
}
