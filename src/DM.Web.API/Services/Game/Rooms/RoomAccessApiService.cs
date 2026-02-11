using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Game.BusinessProcesses.RoomAccesses.Creating;
using DM.Services.Game.BusinessProcesses.RoomAccesses.Deleting;
using DM.Services.Game.BusinessProcesses.RoomAccesses.Updating;
using DM.Services.Game.Dto.Input;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;

namespace DM.Web.API.Services.Game.Rooms;

/// <inheritdoc />
internal class RoomAccessApiService : IRoomAccessApiService
{
    private readonly IRoomAccessCreatingService creatingService;
    private readonly IRoomAccessUpdatingService updatingService;
    private readonly IRoomAccessDeletingService deletingService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public RoomAccessApiService(
        IRoomAccessCreatingService creatingService,
        IRoomAccessUpdatingService updatingService,
        IRoomAccessDeletingService deletingService,
        IMapper mapper)
    {
        this.creatingService = creatingService;
        this.updatingService = updatingService;
        this.deletingService = deletingService;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<RoomAccess>> Create(Guid roomId, RoomAccess access)
    {
        var createRoomAccess = mapper.Map<CreateRoomAccess>(access);
        createRoomAccess.RoomId = roomId;
        var createdRoomAccess = await creatingService.Create(createRoomAccess);
        return new Envelope<RoomAccess>(mapper.Map<RoomAccess>(createdRoomAccess));
    }

    /// <inheritdoc />
    public async Task<Envelope<RoomAccess>> Update(Guid accessId, RoomAccess access)
    {
        var updateRoomAccess = mapper.Map<UpdateRoomAccess>(access);
        updateRoomAccess.AccessId = accessId;
        var updatedRoomAccess = await updatingService.Update(updateRoomAccess);
        return new Envelope<RoomAccess>(mapper.Map<RoomAccess>(updatedRoomAccess));
    }

    /// <inheritdoc />
    public Task Delete(Guid accessId) => deletingService.Delete(accessId);
}
