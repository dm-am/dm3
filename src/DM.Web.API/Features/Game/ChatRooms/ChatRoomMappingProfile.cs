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
        CreateMap<DomainRoom, ChatRoom>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.Id))
            .ForMember(d => d.UnreadCount, opt => opt.Ignore()); // Set in service

        CreateMap<DomainRoomAccess, ChatRoomAccess>()
            .ForMember(d => d.User, s => s.MapFrom(a =>
                a.TargetType == RoomAccessTargetType.Reader ? a.User : null))
            .ForMember(d => d.Character, opt => opt.Ignore()); // Mapped separately if needed
    }
}
