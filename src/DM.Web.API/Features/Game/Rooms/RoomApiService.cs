using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Rooms;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Rooms;

/// <inheritdoc />
internal class RoomApiService : IRoomApiService
{
    private readonly IRoomService _roomService;
    private readonly RoomMapper _mapper;

    /// <inheritdoc />
    public RoomApiService(
        IRoomService roomService,
        RoomMapper mapper)
    {
        _roomService = roomService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Room>> GetAll(Guid gameId)
    {
        var rooms = await _roomService.GetAllAsync(gameId);
        return new ListEnvelope<Room>(rooms.Select(_mapper.ToRoom));
    }

    /// <inheritdoc />
    public async Task<Envelope<Room>> Get(Guid roomId)
    {
        var room = await _roomService.GetAsync(roomId);
        return new Envelope<Room>(_mapper.ToRoom(room));
    }

    /// <inheritdoc />
    public async Task<Envelope<Room>> Create(Guid gameId, CreateRoomRequest room)
    {
        var createRoom = _mapper.ToCreateRoom(room);
        createRoom.GameId = gameId;
        var createdRoom = await _roomService.CreateAsync(createRoom);
        return new Envelope<Room>(_mapper.ToRoom(createdRoom));
    }

    /// <inheritdoc />
    public async Task<Envelope<Room>> Update(Guid roomId, UpdateRoomRequest request)
    {
        var updateRoom = _mapper.ToUpdateRoom(request);
        updateRoom.RoomId = roomId;
        var updatedRoom = await _roomService.UpdateAsync(updateRoom);
        return new Envelope<Room>(_mapper.ToRoom(updatedRoom));
    }

    /// <inheritdoc />
    public Task Delete(Guid roomId) => _roomService.DeleteAsync(roomId);
}
