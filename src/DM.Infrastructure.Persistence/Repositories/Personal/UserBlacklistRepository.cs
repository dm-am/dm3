using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Blacklists;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc />
internal class UserBlacklistRepository : IUserBlacklistRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserBlacklistRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlacklistEntry>> GetBlacklist(Guid ownerId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .Where(b => b.OwnerId == ownerId)
            .OrderByDescending(b => b.CreatedUtc)
            .ProjectTo<BlacklistEntry>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BlacklistEntry?> Get(Guid entryId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .Where(b => b.EntryId == entryId)
            .ProjectTo<BlacklistEntry>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BlacklistEntry?> Find(Guid ownerId, Guid blockedUserId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .Where(b => b.OwnerId == ownerId && b.BlockedUserId == blockedUserId)
            .ProjectTo<BlacklistEntry>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsBlockedAsync(Guid ownerId, Guid blockedUserId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .AnyAsync(b => b.OwnerId == ownerId && b.BlockedUserId == blockedUserId, ct);
    }

    /// <inheritdoc />
    public async Task<bool> HasBlockRelationship(Guid userId1, Guid userId2, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .AnyAsync(b =>
                (b.OwnerId == userId1 && b.BlockedUserId == userId2) ||
                (b.OwnerId == userId2 && b.BlockedUserId == userId1), ct);
    }

    /// <inheritdoc />
    public async Task<BlacklistEntry> Create(CreateBlacklistEntryEntity entry, CancellationToken ct = default)
    {
        var entity = new UserBlacklist
        {
            EntryId = entry.EntryId,
            OwnerId = entry.OwnerId,
            BlockedUserId = entry.BlockedUserId,
            CreatedUtc = entry.CreatedUtc
        };

        _dbContext.UserBlacklists.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        return (await Get(entity.EntryId, ct))!;
    }

    /// <inheritdoc />
    public async Task Delete(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _dbContext.UserBlacklists.FindAsync(new object[] { entryId }, ct);
        if (entry != null)
        {
            _dbContext.UserBlacklists.Remove(entry);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetBlockedUserIdsAsync(Guid ownerId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .Where(b => b.OwnerId == ownerId)
            .Select(b => b.BlockedUserId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// One statement, because the switch and the entries it governs now live in
    /// the same store. This used to be a Mongo read followed by a Postgres read,
    /// with nothing keeping the two consistent.
    /// </remarks>
    public async Task<IReadOnlySet<Guid>> GetBlockedUserIdsIfFlagEnabledAsync(Guid ownerId, UserBlacklistSettings flag, CancellationToken ct = default)
    {
        var blockedIds = await _dbContext.UserBlacklists
            .Where(b => b.OwnerId == ownerId &&
                        _dbContext.Users.Any(u => u.UserId == ownerId &&
                                                  (u.BlacklistSettings & flag) == flag))
            .Select(b => b.BlockedUserId)
            .ToListAsync(ct);

        return blockedIds.ToHashSet();
    }

    /// <inheritdoc />
    /// <remarks>
    /// One statement for the whole audience. The unique index leads with OwnerId,
    /// so the set membership seeks and the equality on BlockedUserId is the second
    /// column of the same index.
    /// </remarks>
    public async Task<IReadOnlySet<Guid>> GetOwnersBlockingAsync(
        Guid blockedUserId, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct = default)
    {
        if (ownerIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var blocking = await _dbContext.UserBlacklists
            .Where(b => b.BlockedUserId == blockedUserId && ownerIds.Contains(b.OwnerId))
            .Select(b => b.OwnerId)
            .ToListAsync(ct);

        return blocking.ToHashSet();
    }

    /// <inheritdoc />
    public async Task<UserBlacklistSettings> GetSettings(Guid userId, CancellationToken ct = default)
    {
        var settings = await _dbContext.Users
            .Where(u => u.UserId == userId)
            .Select(u => (UserBlacklistSettings?)u.BlacklistSettings)
            .FirstOrDefaultAsync(ct);

        return settings ?? UserBlacklistSettings.Default;
    }

    /// <inheritdoc />
    public async Task<UserBlacklistSettings> UpdateSettings(Guid userId, UserBlacklistSettings settings, CancellationToken ct = default)
    {
        await _dbContext.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.BlacklistSettings, settings), ct);

        return settings;
    }
}
