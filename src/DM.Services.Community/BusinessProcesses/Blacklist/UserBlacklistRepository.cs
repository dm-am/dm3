using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.BusinessObjects.Users.Settings;
using DM.Services.DataAccess.MongoIntegration;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace DM.Services.Community.BusinessProcesses.Blacklist;

/// <inheritdoc />
internal class UserBlacklistRepository : MongoCollectionRepository<UserSettings>, IUserBlacklistRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public UserBlacklistRepository(
        DmDbContext dbContext,
        DmMongoClient mongoClient) : base(mongoClient)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserBlacklist>> GetBlacklist(Guid ownerId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .Include(b => b.BlockedUser)
            .Where(b => b.OwnerId == ownerId)
            .OrderByDescending(b => b.CreatedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UserBlacklist?> Get(Guid entryId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .Include(b => b.BlockedUser)
            .FirstOrDefaultAsync(b => b.EntryId == entryId, ct);
    }

    /// <inheritdoc />
    public async Task<UserBlacklist?> Find(Guid ownerId, Guid blockedUserId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .FirstOrDefaultAsync(b => b.OwnerId == ownerId && b.BlockedUserId == blockedUserId, ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsBlocked(Guid ownerId, Guid blockedUserId, CancellationToken ct = default)
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
    public async Task<UserBlacklist> Create(UserBlacklist entry, CancellationToken ct = default)
    {
        _dbContext.UserBlacklists.Add(entry);
        await _dbContext.SaveChangesAsync(ct);
        return entry;
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
    public async Task<IEnumerable<Guid>> GetBlockedUserIds(Guid ownerId, CancellationToken ct = default)
    {
        return await _dbContext.UserBlacklists
            .Where(b => b.OwnerId == ownerId)
            .Select(b => b.BlockedUserId)
            .ToListAsync(ct);
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
                ColorSchema = ColorSchema.Light
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
