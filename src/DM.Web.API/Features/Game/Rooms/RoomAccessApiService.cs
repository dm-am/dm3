using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Rooms;

public class RoomAccessApiService
{
    private readonly IRoomAccessService _roomAccessService;
    private readonly RoomMapper _mapper;

    public RoomAccessApiService(
        IRoomAccessService roomAccessService,
        RoomMapper mapper)
    {
        _roomAccessService = roomAccessService;
        _mapper = mapper;
    }

    public async Task<Envelope<RoomAccess>> Create(Guid roomId, RoomAccess access)
    {
        var createRoomAccess = RoomMapper.ToCreateRoomAccess(access);
        createRoomAccess.RoomId = roomId;
        var createdRoomAccess = await _roomAccessService.CreateAsync(createRoomAccess);
        return new Envelope<RoomAccess>(_mapper.ToRoomAccess(createdRoomAccess));
    }

    public async Task<Envelope<RoomAccess>> Update(Guid accessId, UpdateRoomAccessRequest request)
    {
        var updateRoomAccess = _mapper.ToUpdateRoomAccess(request);
        updateRoomAccess.AccessId = accessId;
        var updatedRoomAccess = await _roomAccessService.UpdateAsync(updateRoomAccess);
        return new Envelope<RoomAccess>(_mapper.ToRoomAccess(updatedRoomAccess));
    }

    public Task Delete(Guid accessId) => _roomAccessService.DeleteAsync(accessId);
}
