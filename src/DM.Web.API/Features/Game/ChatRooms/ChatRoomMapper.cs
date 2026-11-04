using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using Riok.Mapperly.Abstractions;
using DomainRoom = DM.Domain.Game.Features.Rooms.Room;
using DomainRoomAccess = DM.Domain.Game.Features.RoomAccesses.RoomAccess;

namespace DM.Web.API.Features.Game.ChatRooms;

/// <summary>
/// Compile-time mapper for chat room models
/// </summary>
[Mapper]
public partial class ChatRoomMapper
{
    private readonly UserMapper _userMapper;

    public ChatRoomMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain room to the chat room DTO. Its unread messages are the room's
    /// unread counter, the half of the pair the marker is keyed by.
    /// </summary>
    [MapProperty(nameof(DomainRoom.UnreadPostsCount), nameof(ChatRoom.UnreadCount))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ChatRoom ToChatRoom(DomainRoom room);

    // Only reader grants carry a user.
    private ChatRoomAccess ToChatRoomAccess(DomainRoomAccess access) => new()
    {
        Id = access.Id,
        User = access.TargetType == RoomAccessTargetType.Reader
            ? _userMapper.ToUser(access.User)
            : null,
    };
}
