using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Internal;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;

namespace DM.Services.Game.BusinessProcesses.Rooms.Creating;

/// <inheritdoc />
internal class RoomCreatingRepository : IRoomCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Room> Create(DbRoom room, IUpdateBuilder<DbRoom>? updateLastRoom)
    {
        _dbContext.Rooms.Add(room);
        updateLastRoom?.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Rooms
            .Where(r => r.RoomId == room.RoomId)
            .ProjectTo<Room>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public Task<RoomOrderInfo?> GetLastRoomInfo(Guid gameId)
    {
        return _dbContext.Rooms
            .Where(r => !r.IsRemoved && r.GameId == gameId)
            .OrderByDescending(r => r.OrderNumber)
            .ProjectTo<RoomOrderInfo>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }
}