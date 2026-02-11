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

namespace DM.Services.Game.BusinessProcesses.Rooms.Reading;

/// <inheritdoc />
internal class RoomReadingRepository : IRoomReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Room>> GetAllAvailable(Guid gameId, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(r => r.GameId == gameId)
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .OrderBy(r => r.OrderNumber)
            .ProjectTo<Room>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public Task<Room?> GetAvailable(Guid roomId, Guid userId)
    {
        return _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .ProjectTo<Room>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }
}