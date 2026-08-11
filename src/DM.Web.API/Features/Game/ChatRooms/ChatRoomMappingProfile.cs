using AutoMapper;
using DM.Domain.Core.Enums;
using DomainRoom = DM.Domain.Game.Features.Games.Room;
using DomainRoomAccess = DM.Domain.Game.Features.Games.RoomAccess;

namespace DM.Web.API.Features.Game.ChatRooms;

/// <summary>
/// Mapping profile for chat room models
/// </summary>
internal class ChatRoomMappingProfile : Profile
{
    /// <inheritdoc />
    public ChatRoomMappingProfile()
    {
        // A chat room's unread messages are its room's unread counter: the room
        // is the half of the pair the marker is keyed by, and RoomService fills
        // the field on every read this profile maps from.
        CreateMap<DomainRoom, ChatRoom>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.Id))
            .ForMember(d => d.UnreadCount, s => s.MapFrom(r => r.UnreadPostsCount));

        CreateMap<DomainRoomAccess, ChatRoomAccess>()
            .ForMember(d => d.User, s => s.MapFrom(a =>
                a.TargetType == RoomAccessTargetType.Reader ? a.User : null))
            .ForMember(d => d.Character, opt => opt.Ignore()); // Mapped separately if needed
    }
}
