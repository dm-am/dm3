using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Blacklists;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class GameBlacklistRepository : IGameBlacklistRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GameBlacklistRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> Get(Guid gameId) => await _dbContext.GameBlacklists
        .Where(b => b.GameId == gameId)
        .Select(b => b.BlockedUser)
        .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
        .ToArrayAsync();

    /// <inheritdoc />
    public async Task<GeneralUser> Add(Guid gameId, Guid blockedUserId, Guid blockedByUserId)
    {
        var blacklistEntry = new GameBlacklist
        {
            EntryId = Guid.NewGuid(),
            GameId = gameId,
            BlockedUserId = blockedUserId,
            BlockedByUserId = blockedByUserId,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _dbContext.GameBlacklists.Add(blacklistEntry);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.GameBlacklists
            .Where(b => b.EntryId == blacklistEntry.EntryId)
            .Select(b => b.BlockedUser)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
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
