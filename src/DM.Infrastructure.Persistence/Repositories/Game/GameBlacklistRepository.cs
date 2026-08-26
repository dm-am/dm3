using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Blacklists;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using Microsoft.EntityFrameworkCore;

using DM.Infrastructure.Persistence.Shared.Users;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class GameBlacklistRepository : IGameBlacklistRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public GameBlacklistRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> Get(Guid gameId) => await _dbContext.GameBlacklists
        .Where(b => b.GameId == gameId)
        .Select(b => b.BlockedUser)
        .ProjectToGeneralUser()
        .ToArrayAsync();

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task<int> CopyFromPersonalBlacklist(Guid gameId, Guid ownerId, CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;

        var personal = await _dbContext.UserBlacklists
            .Where(ub => ub.OwnerId == ownerId)
            .Select(ub => ub.BlockedUserId)
            .ToListAsync(ct);

        var already = await _dbContext.GameBlacklists
            .Where(gb => gb.GameId == gameId)
            .Select(gb => gb.BlockedUserId)
            .ToListAsync(ct);

        var toAdd = personal.Except(already).ToList();

        foreach (var blockedUserId in toAdd)
        {
            _dbContext.GameBlacklists.Add(new GameBlacklist
            {
                EntryId = _guidFactory.Create(),
                GameId = gameId,
                BlockedUserId = blockedUserId,
                BlockedByUserId = ownerId,
                CreatedUtc = now
            });
        }

        if (toAdd.Count > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
        }

        return toAdd.Count;
    }

    public async Task<GeneralUser> Add(Guid gameId, Guid blockedUserId, Guid blockedByUserId)
    {
        var blacklistEntry = new GameBlacklist
        {
            EntryId = _guidFactory.Create(),
            GameId = gameId,
            BlockedUserId = blockedUserId,
            BlockedByUserId = blockedByUserId,
            CreatedUtc = _dateTimeProvider.Now
        };

        _dbContext.GameBlacklists.Add(blacklistEntry);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.GameBlacklists
            .Where(b => b.EntryId == blacklistEntry.EntryId)
            .Select(b => b.BlockedUser)
            .ProjectToGeneralUser()
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task Remove(Guid gameId, Guid userId)
    {
        var entry = await _dbContext.GameBlacklists
            .FirstOrDefaultAsync(b => b.GameId == gameId && b.BlockedUserId == userId);
        if (entry != null)
        {
            _dbContext.GameBlacklists.Remove(entry);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsBlocked(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.GameBlacklists
            .AnyAsync(b => b.GameId == gameId && b.BlockedUserId == userId, ct);
    }
}
