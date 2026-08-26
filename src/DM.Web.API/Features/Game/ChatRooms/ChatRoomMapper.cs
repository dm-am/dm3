using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using Riok.Mapperly.Abstractions;
using DomainRoom = DM.Domain.Game.Features.Games.Room;
using DomainRoomAccess = DM.Domain.Game.Features.Games.RoomAccess;

namespace DM.Web.API.Features.Game.ChatRooms;

/// <summary>
/// Compile-time mapper for chat room models
/// </summary>
[Mapper]
internal partial class ChatRoomMapper
{
    private readonly UserMapper _userMapper;

    public ChatRoomMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain room to the chat room DTO. A chat room's unread messages are
    /// its room's unread counter: the room is the half of the pair the marker
    /// is keyed by, and RoomService fills the field on every read this maps
    /// from.
    /// </summary>
    [MapProperty(nameof(DomainRoom.UnreadPostsCount), nameof(ChatRoom.UnreadCount))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ChatRoom ToChatRoom(DomainRoom room);

    // Only reader grants carry a user; the character half is mapped
    // separately if a surface ever needs it.
    private ChatRoomAccess ToChatRoomAccess(DomainRoomAccess access) => new()
    {
        Id = access.Id,
        User = access.TargetType == RoomAccessTargetType.Reader
            ? _userMapper.ToUser(access.User)
            : null,
        Character = null,
    };
}
