using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Core.Dto.Enums;
using DM.Services.Game.BusinessProcesses.Rooms.Creating;
using DM.Services.Game.BusinessProcesses.Rooms.Deleting;
using DM.Services.Game.BusinessProcesses.Rooms.Reading;
using DM.Services.Game.BusinessProcesses.Rooms.Updating;
using DM.Services.Game.Dto.Input;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;

namespace DM.Web.API.Services.Game.Rooms;

/// <inheritdoc />
internal class RoomApiService : IRoomApiService
{
    private readonly IRoomReadingService readingService;
    private readonly IRoomCreatingService creatingService;
    private readonly IRoomUpdatingService updatingService;
    private readonly IRoomDeletingService deletingService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public RoomApiService(
        IRoomReadingService readingService,
        IRoomCreatingService creatingService,
        IRoomUpdatingService updatingService,
        IRoomDeletingService deletingService,
        IMapper mapper)
    {
        this.readingService = readingService;
        this.creatingService = creatingService;
        this.updatingService = updatingService;
        this.deletingService = deletingService;
        this.mapper = mapper;
    }
        
    /// <inheritdoc />
    public async Task<ListEnvelope<Room>> GetAll(Guid gameId)
    {
        var rooms = await readingService.GetAll(gameId);
        return new ListEnvelope<Room>(rooms.Select(mapper.Map<Room>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Room>> GetByType(Guid gameId, RoomType roomType)
    {
        var rooms = await readingService.GetAll(gameId);
        var filtered = rooms.Where(r => r.Type == roomType);
        return new ListEnvelope<Room>(filtered.Select(mapper.Map<Room>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Room>> Get(Guid roomId)
    {
        var room = await readingService.Get(roomId);
        return new Envelope<Room>(mapper.Map<Room>(room));
    }

    /// <inheritdoc />
    public async Task<Envelope<Room>> Create(Guid gameId, CreateRoomRequest room)
    {
        var createRoom = mapper.Map<CreateRoom>(room);
        createRoom.GameId = gameId;
        var createdRoom = await creatingService.Create(createRoom);
        return new Envelope<Room>(mapper.Map<Room>(createdRoom));
    }

    /// <inheritdoc />
    public async Task<Envelope<Room>> Update(Guid roomId, Room room)
    {
        var updateRoom = mapper.Map<UpdateRoom>(room);
        updateRoom.RoomId = roomId;
        var updatedRoom = await updatingService.Update(updateRoom);
        return new Envelope<Room>(mapper.Map<Room>(updatedRoom));
    }

    /// <inheritdoc />
    public Task Delete(Guid roomId) => deletingService.Delete(roomId);
}