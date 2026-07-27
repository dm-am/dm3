using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using DbUserEndorsement = DM.Infrastructure.Persistence.Entities.Community.UserEndorsement;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class UserEndorsementRepository : IUserEndorsementRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public UserEndorsementRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> CountAsync(Guid targetUserId) => _dbContext.UserEndorsements
        .Where(e => e.TargetUserId == targetUserId && !e.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<UserEndorsement>> GetAsync(Guid targetUserId, PagingData paging) =>
        await _dbContext.UserEndorsements
            .Where(e => e.TargetUserId == targetUserId && !e.IsRemoved)
            .OrderByDescending(e => e.CreatedUtc)
            .ThenByDescending(e => e.UserEndorsementId)
            .Page(paging)
            .ProjectTo<UserEndorsement>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<UserEndorsement?> GetAsync(Guid id) => _dbContext.UserEndorsements
        .Where(e => !e.IsRemoved && e.UserEndorsementId == id)
        .ProjectTo<UserEndorsement>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<UserEndorsement?> GetByAuthorAsync(Guid targetUserId, Guid authorId) => _dbContext.UserEndorsements
        .Where(e => e.TargetUserId == targetUserId &&
                    e.AuthorId == authorId &&
                    !e.IsRemoved)
        .ProjectTo<UserEndorsement>(_mapper.ConfigurationProvider)
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
            .ProjectTo<UserEndorsement>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <summary>
    /// Shared predicate block: filtering by author / recipient plus
    /// substring search. Search ILIKEs over the text, author name and
    /// recipient name at once — three SQL conditions joined by OR. This is
    /// the equivalent of "find where X is mentioned" without separate search buckets.
    /// </summary>
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
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(e =>
                EF.Functions.ILike(e.Text, pattern) ||
                EF.Functions.ILike(e.Author.Username, pattern) ||
                EF.Functions.ILike(e.TargetUser.Username, pattern));
        }

        return query;
    }

    /// <summary>
    /// List sorting. Supported fields are kept in sync with the FE
    /// ReviewsFilter SORT_OPTIONS ("created", "author") — adding
    /// a new option on the FE without a matching case here silently
    /// falls into the default order, so the SSOT is kept in this switch.
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
            var byRecipient = filter?.AuthorId != null;
            var ordered = (byRecipient, desc) switch
            {
                (true, true) => query.OrderByDescending(e => e.TargetUser.Username),
                (true, false) => query.OrderBy(e => e.TargetUser.Username),
                (false, true) => query.OrderByDescending(e => e.Author.Username),
                (false, false) => query.OrderBy(e => e.Author.Username),
            };
            return desc
                ? ordered.ThenByDescending(e => e.CreatedUtc).ThenByDescending(e => e.UserEndorsementId)
                : ordered.ThenBy(e => e.CreatedUtc).ThenBy(e => e.UserEndorsementId);
        }

        return desc
            ? query.OrderByDescending(e => e.CreatedUtc).ThenByDescending(e => e.UserEndorsementId)
            : query.OrderBy(e => e.CreatedUtc).ThenBy(e => e.UserEndorsementId);
    }

    // ═══ WRITE ═══

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
        await _dbContext.SaveChangesAsync();

        return await _dbContext.UserEndorsements
            .Where(e => e.UserEndorsementId == dbEndorsement.UserEndorsementId)
            .ProjectTo<UserEndorsement>(_mapper.ConfigurationProvider)
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
        if (entity.IsRemoved.HasValue)
            dbEndorsement.IsRemoved = entity.IsRemoved.Value;
        if (entity.ModifiedUtc.HasValue)
            dbEndorsement.ModifiedUtc = entity.ModifiedUtc.Value;
        if (entity.ModifiedByUserId.HasValue)
            dbEndorsement.ModifiedByUserId = entity.ModifiedByUserId;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.UserEndorsements
            .Where(e => e.UserEndorsementId == entity.EndorsementId)
            .ProjectTo<UserEndorsement>(_mapper.ConfigurationProvider)
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
