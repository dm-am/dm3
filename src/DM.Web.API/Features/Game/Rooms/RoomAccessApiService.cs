using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Rooms;

/// <inheritdoc />
internal class RoomAccessApiService : IRoomAccessApiService
{
    private readonly IRoomAccessService _roomAccessService;
    private readonly RoomMapper _mapper;

    /// <inheritdoc />
    public RoomAccessApiService(
        IRoomAccessService roomAccessService,
        RoomMapper mapper)
    {
        _roomAccessService = roomAccessService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<RoomAccess>> Create(Guid roomId, RoomAccess access)
    {
        var createRoomAccess = _mapper.ToCreateRoomAccess(access);
        createRoomAccess.RoomId = roomId;
        var createdRoomAccess = await _roomAccessService.CreateAsync(createRoomAccess);
        return new Envelope<RoomAccess>(_mapper.ToRoomAccess(createdRoomAccess));
    }

    /// <inheritdoc />
    public async Task<Envelope<RoomAccess>> Update(Guid accessId, UpdateRoomAccessRequest request)
    {
        var updateRoomAccess = _mapper.ToUpdateRoomAccess(request);
        updateRoomAccess.AccessId = accessId;
        var updatedRoomAccess = await _roomAccessService.UpdateAsync(updateRoomAccess);
        return new Envelope<RoomAccess>(_mapper.ToRoomAccess(updatedRoomAccess));
    }

    /// <inheritdoc />
    public Task Delete(Guid accessId) => _roomAccessService.DeleteAsync(accessId);
}
