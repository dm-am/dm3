using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Web.API.Shared.Dto;
using CreateRoomAccess = DM.Domain.Game.Features.RoomAccesses.CreateRoomAccess;
using UpdateRoomAccess = DM.Domain.Game.Features.RoomAccesses.UpdateRoomAccess;

namespace DM.Web.API.Features.Game.Rooms;

/// <inheritdoc />
internal class RoomAccessApiService : IRoomAccessApiService
{
    private readonly IRoomAccessService _roomAccessService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomAccessApiService(
        IRoomAccessService roomAccessService,
        IMapper mapper)
    {
        _roomAccessService = roomAccessService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<RoomAccess>> Create(Guid roomId, RoomAccess access)
    {
        var createRoomAccess = _mapper.Map<CreateRoomAccess>(access);
        createRoomAccess.RoomId = roomId;
        var createdRoomAccess = await _roomAccessService.CreateAsync(createRoomAccess);
        return new Envelope<RoomAccess>(_mapper.Map<RoomAccess>(createdRoomAccess));
    }

    /// <inheritdoc />
    public async Task<Envelope<RoomAccess>> Update(Guid accessId, UpdateRoomAccessRequest request)
    {
        var updateRoomAccess = _mapper.Map<UpdateRoomAccess>(request);
        updateRoomAccess.AccessId = accessId;
        var updatedRoomAccess = await _roomAccessService.UpdateAsync(updateRoomAccess);
        return new Envelope<RoomAccess>(_mapper.Map<RoomAccess>(updatedRoomAccess));
    }

    /// <inheritdoc />
    public Task Delete(Guid accessId) => _roomAccessService.DeleteAsync(accessId);
}
