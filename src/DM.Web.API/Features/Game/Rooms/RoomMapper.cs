using System;
using DM.Domain.Core.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using Riok.Mapperly.Abstractions;
using DomainRoom = DM.Domain.Game.Features.Games.Room;
using DomainRoomSettings = DM.Domain.Game.Features.Games.RoomSettings;
using DomainRoomAccess = DM.Domain.Game.Features.Games.RoomAccess;
using DomainPostPendency = DM.Domain.Game.Features.Games.PostPendency;
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
internal partial class RoomMapper
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
    /// Domain room to its response DTO. Access answers from AccessType -
    /// it used to be ignored, which left every room in game-details responses
    /// with `access = null` and made downstream tooltip logic default every
    /// room to "open" and leak the wrong character list.
    /// </summary>
    // Rooms are returned nested under their game; the domain Room carries
    // only GameId (no GameRef nav), so this back-reference stays null
    // instead of being re-hydrated per room.
    [MapperIgnoreTarget(nameof(Room.Game))]
    [MapProperty(nameof(DomainRoom.AccessType), nameof(Room.Access))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial Room ToRoom(DomainRoom room);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial RoomSettings ToRoomSettings(DomainRoomSettings settings);

    /// <summary>
    /// Domain access grant to its response DTO. The policy used to be
    /// ignored, so every access of every response answered null: the screen
    /// that grants access could not show which grant it had just made, and
    /// the same value now decides who may write.
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
    /// service sets it; ChatId is server-managed (linked chat room) and the
    /// API Room DTO does not expose it, so an update never reassigns it.
    /// The settings toggles flatten from the nested Settings block and the
    /// repository applies each only when its nullable is set - a sparse
    /// PATCH with Settings omitted leaves them null (== unchanged).
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdateRoom.RoomId))]
    [MapperIgnoreTarget(nameof(DomainUpdateRoom.ChatId))]
    [MapperIgnoreTarget(nameof(DomainUpdateRoom.IsRemoved))]
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
    public DomainCreateRoomAccess ToCreateRoomAccess(RoomAccess access) => new()
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
    /// Pendency body to the write model. RoomId comes from the route, the
    /// service overwrites whatever the body carried.
    /// </summary>
    public DomainCreatePostPendency ToCreatePostPendency(PostPendency pendency) => new()
    {
        RoomId = pendency.RoomId,
        CharacterId = pendency.CharacterId,
        WaitingForUsername = pendency.WaitingFor?.Username!
    };

    // Wire-compatible with the AutoMapper path: a room with no predecessor
    // serializes to JSON null either way, so the bare null stays bare
    // instead of being wrapped into Optional.WithValue(null).
    private static Optional<Guid>? ToOptional(Guid? value) =>
        value == null ? null : Optional<Guid>.WithValue(value);
}
