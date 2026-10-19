using System;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using Riok.Mapperly.Abstractions;
using DomainRoom = DM.Domain.Game.Features.Rooms.Room;
using DomainRoomSettings = DM.Domain.Game.Features.Rooms.RoomSettings;
using DomainRoomAccess = DM.Domain.Game.Features.RoomAccesses.RoomAccess;
using DomainPostPendency = DM.Domain.Game.Features.PostPendencies.PostPendency;
using DomainCreateRoom = DM.Domain.Game.Features.Rooms.CreateRoom;
using DomainUpdateRoom = DM.Domain.Game.Features.Rooms.UpdateRoom;
using DomainCreateRoomAccess = DM.Domain.Game.Features.RoomAccesses.CreateRoomAccess;
using DomainUpdateRoomAccess = DM.Domain.Game.Features.RoomAccesses.UpdateRoomAccess;
using DomainCreatePostPendency = DM.Domain.Game.Features.PostPendencies.CreatePostPendency;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// Compile-time mapper for room models: the room itself, its access grants
/// and its post pendencies.
/// </summary>
[Mapper]
public partial class RoomMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    [UseMapper]
    private readonly CharacterMapper _characterMapper;

    public RoomMapper(UserMapper userMapper, CharacterMapper characterMapper)
    {
        _userMapper = userMapper;
        _characterMapper = characterMapper;
    }

    /// <summary>
    /// Domain room to its response DTO
    /// </summary>
    // Rooms come nested under their game, and the domain room carries only GameId.
    [MapperIgnoreTarget(nameof(Room.Game))]
    [MapProperty(nameof(DomainRoom.AccessType), nameof(Room.Access))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial Room ToRoom(DomainRoom room);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial RoomSettings ToRoomSettings(DomainRoomSettings settings);

    /// <summary>
    /// Domain access grant to its response DTO
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial RoomAccess ToRoomAccess(DomainRoomAccess access);

    [MapProperty(nameof(DomainPostPendency.WaitingForUser), nameof(PostPendency.WaitingFor))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial PostPendency ToPostPendency(DomainPostPendency pendency);

    /// <summary>
    /// Create request to the write model. GameId comes from the route, the
    /// service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainCreateRoom.GameId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainCreateRoom ToCreateRoom(CreateRoomRequest request);

    /// <summary>
    /// Update request to the write model. RoomId comes from the route, the
    /// service sets it; ChatId is the server's. An omitted Settings block leaves
    /// the toggles null, unchanged.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdateRoom.RoomId))]
    [MapperIgnoreTarget(nameof(DomainUpdateRoom.ChatId))]
    [MapProperty(nameof(UpdateRoomRequest.Access), nameof(DomainUpdateRoom.AccessType))]
    [MapProperty(
        [nameof(UpdateRoomRequest.Settings), nameof(RoomSettings.ViewPrivateText)],
        [nameof(DomainUpdateRoom.ViewPrivateText)])]
    [MapProperty(
        [nameof(UpdateRoomRequest.Settings), nameof(RoomSettings.ViewDiceResults)],
        [nameof(DomainUpdateRoom.ViewDiceResults)])]
    [MapProperty(
        [nameof(UpdateRoomRequest.Settings), nameof(RoomSettings.DiceEnabled)],
        [nameof(DomainUpdateRoom.DiceEnabled)])]
    [MapProperty(
        [nameof(UpdateRoomRequest.Settings), nameof(RoomSettings.HiddenWithoutAccess)],
        [nameof(DomainUpdateRoom.HiddenWithoutAccess)])]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainUpdateRoom ToUpdateRoom(UpdateRoomRequest request);

    /// <summary>
    /// Access grant body to the write model. The read shape doubles as the
    /// create body: the character or reader the grant is for arrives as a
    /// nested object, of which only the identifying member is read. RoomId
    /// comes from the route, the service sets it.
    /// </summary>
    public static DomainCreateRoomAccess ToCreateRoomAccess(RoomAccess access) => new()
    {
        CharacterId = access.Character?.Id,
        ReaderUsername = access.User?.Username!,
        Policy = access.Policy ?? default
    };

    /// <summary>
    /// Access update request to the write model. AccessId comes from the
    /// route, the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdateRoomAccess.AccessId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainUpdateRoomAccess ToUpdateRoomAccess(UpdateRoomAccessRequest request);

    /// <summary>
    /// Pendency request to the write model, the room taken from the route.
    /// </summary>
    public static DomainCreatePostPendency ToCreatePostPendency(Guid roomId, CreatePostPendencyRequest request) => new()
    {
        RoomId = roomId,
        CharacterId = request.CharacterId
    };
}
