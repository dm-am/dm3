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
    /// Общий predicate-блок: фильтрация по автору / получателю и
    /// подстрочный поиск. Search ILIKE'ит по тексту, имени автора и
    /// имени получателя одновременно — три SQL-условия через OR. Это
    /// эквивалент «найди где упоминается X» без отдельных бакетов поиска.
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
    /// Сортировка списка. Поддерживаемые поля синхронизированы с FE
    /// ReviewsFilter SORT_OPTIONS ("created", "author") — добавление
    /// новой опции на FE без соответствующего case'а здесь молча
    /// упадет в default-порядок, поэтому держим SSOT в этом switch.
    /// </summary>
    private static IOrderedQueryable<DbUserEndorsement> ApplySort(
        IQueryable<DbUserEndorsement> query, UserEndorsementFilter? filter)
    {
        var sortBy = (filter?.SortBy ?? "created").ToLowerInvariant();
        var desc = string.IsNullOrEmpty(filter?.SortOrder) ||
                   filter.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy, desc) switch
        {
            ("author", true) => query.OrderByDescending(e => e.Author.Username),
            ("author", false) => query.OrderBy(e => e.Author.Username),
            (_, true) => query.OrderByDescending(e => e.CreatedUtc),
            (_, false) => query.OrderBy(e => e.CreatedUtc),
        };
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
