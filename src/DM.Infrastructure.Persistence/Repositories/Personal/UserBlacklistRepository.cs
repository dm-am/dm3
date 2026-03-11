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
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.MongoIntegration;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc />
internal class UserBlacklistRepository : MongoCollectionRepository<UserSettings>, IUserBlacklistRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserBlacklistRepository(
        DmDbContext dbContext,
        DmMongoClient mongoClient,
        IMapper mapper) : base(mongoClient)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlacklistEntry>> GetBlacklist(Guid ownerId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .Include(b => b.BlockedUser)
            .Where(b => b.OwnerId == ownerId)
            .OrderByDescending(b => b.CreatedUtc)
            .ProjectTo<BlacklistEntry>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BlacklistEntry?> Get(Guid entryId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .Include(b => b.BlockedUser)
            .Where(b => b.EntryId == entryId)
            .ProjectTo<BlacklistEntry>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BlacklistEntry?> Find(Guid ownerId, Guid blockedUserId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .Include(b => b.BlockedUser)
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
    public async Task<IReadOnlySet<Guid>> GetBlockedUserIdsIfFlagEnabledAsync(Guid ownerId, UserBlacklistSettings flag, CancellationToken ct = default)
    {
        var settings = await GetSettings(ownerId, ct);
        if (!settings.HasFlag(flag))
        {
            return new HashSet<Guid>();
        }

        var blockedIds = await _dbContext.UserBlacklists
            .Where(b => b.OwnerId == ownerId)
            .Select(b => b.BlockedUserId)
            .ToListAsync(ct);

        return blockedIds.ToHashSet();
    }

    /// <inheritdoc />
    public async Task<UserBlacklistSettings> GetSettings(Guid userId, CancellationToken ct = default)
    {
        var userSettings = await Collection
            .Find(Filter.Eq(u => u.UserId, userId))
            .FirstOrDefaultAsync(ct);

        return userSettings?.BlacklistSettings ?? UserBlacklistSettings.Default;
    }

    /// <inheritdoc />
    public async Task<UserBlacklistSettings> UpdateSettings(Guid userId, UserBlacklistSettings settings, CancellationToken ct = default)
    {
        var existingSettings = await Collection
            .Find(Filter.Eq(u => u.UserId, userId))
            .FirstOrDefaultAsync(ct);

        if (existingSettings == null)
        {
            // Create new settings document with sensible defaults
            var newSettings = new UserSettings
            {
                UserId = userId,
                BlacklistSettings = settings,
                Paging = new PagingSettings
                {
                    TopicsPerPage = 10,
                    CommentsPerPage = 10,
                    PostsPerPage = 10,
                    MessagesPerPage = 10,
                    EntitiesPerPage = 10
                },
                Theme = Theme.Light
            };
            await Collection.InsertOneAsync(newSettings, cancellationToken: ct);
        }
        else
        {
            // Update existing settings
            var update = Builders<UserSettings>.Update.Set(s => s.BlacklistSettings, settings);
            await Collection.UpdateOneAsync(Filter.Eq(u => u.UserId, userId), update, cancellationToken: ct);
        }

        return settings;
    }
}
