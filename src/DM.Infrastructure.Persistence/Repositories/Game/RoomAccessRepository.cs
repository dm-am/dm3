using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IRoomAccessRepository" />
internal class RoomAccessRepository : IRoomAccessRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public RoomAccessRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    #region Read

    /// <inheritdoc />
    public async Task<IEnumerable<RoomAccess>> GetGameAccesses(Guid gameId, Guid userId)
    {
        return await _dbContext.Rooms
            .TagWith("DM.RoomAccess.GetGameAccesses")
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.GameId == gameId)
            .SelectMany(r => r.RoomAccesses)
            .ProjectTo<RoomAccess>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RoomAccess>> GetRoomAccesses(Guid roomId, Guid userId)
    {
        return await _dbContext.Rooms
            .TagWith("DM.RoomAccess.GetRoomAccesses")
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.RoomId == roomId)
            .SelectMany(r => r.RoomAccesses)
            .ProjectTo<RoomAccess>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reached through the rooms, like the listing above it. An access record
    /// names a person who may enter a private room, so answering by identifier
    /// alone told anyone who asked who has the keys: the reader argument was
    /// taken and dropped, and the one caller that reads without writing does no
    /// checking of its own afterwards.
    /// </remarks>
    public async Task<RoomAccess?> GetAccess(Guid accessId, Guid userId)
    {
        return await _dbContext.Rooms
            .TagWith("DM.RoomAccess.GetAccess")
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .SelectMany(r => r.RoomAccesses)
            .Where(a => a.AccessId == accessId)
            .ProjectTo<RoomAccess>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<Guid?> FindReaderUserId(Guid gameId, string readerUsername)
    {
        var readerUsernameLower = readerUsername.ToLower();
        var userWrapper = await _dbContext.Subscriptions
            .TagWith("DM.RoomAccess.FindReaderUserId")
            .Where(s => s.TargetType == SubscriptionTargetType.Game &&
                       s.TargetId == gameId &&
                       s.Subscriber.Username.ToLower() == readerUsernameLower)
            .Select(s => new { s.SubscriberId })
            .FirstOrDefaultAsync();

        return userWrapper?.SubscriberId;
    }

    /// <inheritdoc />
    public async Task<Guid?> FindCharacterGameId(Guid characterId)
    {
        var gameWrapper = await _dbContext.Characters
            .TagWith("DM.RoomAccess.FindCharacterGameId")
            .Where(c => c.CharacterId == characterId)
            .Select(c => new { c.GameId })
            .FirstOrDefaultAsync();

        return gameWrapper?.GameId;
    }

    /// <inheritdoc />
    public Task<bool> AccessExists(Guid roomId, Guid? characterId, Guid? readerUserId)
    {
        return _dbContext.RoomAccesses
            .TagWith("DM.RoomAccess.AccessExists")
            .AnyAsync(a => a.RoomId == roomId &&
                ((characterId != null && a.CharacterId == characterId) ||
                 (readerUserId != null && a.ReaderUserId == readerUserId)));
    }

    #endregion

    #region Write

    /// <inheritdoc />
    public async Task<RoomAccess> Create(CreateRoomAccessEntity entity)
    {
        var dbAccess = new DbRoomAccess
        {
            AccessId = entity.AccessId,
            RoomId = entity.RoomId,
            CharacterId = entity.CharacterId,
            ReaderUserId = entity.ReaderUserId,
            Policy = entity.Policy
        };

        _dbContext.RoomAccesses.Add(dbAccess);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.RoomAccesses
            .TagWith("DM.RoomAccess.Created")
            .Where(l => l.AccessId == entity.AccessId)
            .ProjectTo<RoomAccess>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<RoomAccess> Update(UpdateRoomAccessEntity entity)
    {
        var access = await _dbContext.RoomAccesses.FindAsync(entity.AccessId);
        if (access == null)
            throw new InvalidOperationException($"RoomAccess {entity.AccessId} not found");

        access.Policy = entity.Policy;
        await _dbContext.SaveChangesAsync();

        return await _dbContext.RoomAccesses
            .TagWith("DM.RoomAccess.Updated")
            .Where(l => l.AccessId == entity.AccessId)
            .ProjectTo<RoomAccess>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid accessId)
    {
        var access = await _dbContext.RoomAccesses.FindAsync(accessId);
        if (access != null)
        {
            _dbContext.RoomAccesses.Remove(access);
            await _dbContext.SaveChangesAsync();
        }
    }

    #endregion
}
