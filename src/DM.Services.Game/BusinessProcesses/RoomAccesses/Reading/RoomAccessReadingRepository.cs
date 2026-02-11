using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.Game.BusinessProcesses.Shared;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Reading;

/// <inheritdoc />
internal class RoomAccessReadingRepository : IRoomAccessReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomAccessReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RoomAccess>> GetGameAccesses(Guid gameId, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.GameId == gameId)
            .SelectMany(r => r.RoomAccesses)
            .ProjectTo<RoomAccess>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RoomAccess>> GetRoomAccesses(Guid roomId, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .SelectMany(r => r.RoomAccesses)
            .ProjectTo<RoomAccess>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<RoomAccess?> GetAccess(Guid accessId, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .SelectMany(r => r.RoomAccesses)
            .Where(l => l.AccessId == accessId)
            .ProjectTo<RoomAccess>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }
}