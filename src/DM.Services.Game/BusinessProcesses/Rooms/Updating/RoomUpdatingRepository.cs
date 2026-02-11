using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.BusinessProcesses.Shared;
using DM.Services.Game.Dto.Internal;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;

namespace DM.Services.Game.BusinessProcesses.Rooms.Updating;

/// <inheritdoc />
internal class RoomUpdatingRepository : IRoomUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<RoomToUpdate?> GetRoom(Guid roomId, Guid userId)
    {
        return _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .ProjectTo<RoomToUpdate>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }

    /// <inheritdoc />
    public Task<RoomNeighbours> GetNeighbours(Guid roomId)
    {
        return _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .ProjectTo<RoomNeighbours>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<Room> Update(
        IUpdateBuilder<DbRoom> updateRoom,
        IUpdateBuilder<DbRoom>? updateOldPreviousRoom,
        IUpdateBuilder<DbRoom>? updateOldNextRoom,
        IUpdateBuilder<DbRoom>? updateNewPreviousRoom,
        IUpdateBuilder<DbRoom>? updateNewNextRoom)
    {
        var roomId = updateRoom.AttachTo(_dbContext);
        updateOldPreviousRoom?.AttachTo(_dbContext);
        updateOldNextRoom?.AttachTo(_dbContext);
        updateNewPreviousRoom?.AttachTo(_dbContext);
        updateNewNextRoom?.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .ProjectTo<Room>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }


    /// <inheritdoc />
    public Task<RoomOrderInfo?> GetFirstRoomInfo(Guid gameId)
    {
        return _dbContext.Rooms
            .Where(r => !r.IsRemoved && r.GameId == gameId)
            .OrderBy(r => r.OrderNumber)
            .ProjectTo<RoomOrderInfo>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }
}