using System;
using AutoMapper;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Domain.Game.Features.PostPendencies;
using DM.Web.API.Features.Game.Characters;
using DomainRoom = DM.Domain.Game.Features.Games.Room;
using DomainRoomSettings = DM.Domain.Game.Features.Games.RoomSettings;
using DomainRoomAccess = DM.Domain.Game.Features.Games.RoomAccess;
using DomainPostPendency = DM.Domain.Game.Features.Games.PostPendency;
using DomainCreatePostPendency = DM.Domain.Game.Features.PostPendencies.CreatePostPendency;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// Mapping profile for room models
/// </summary>
internal class RoomMappingProfile : Profile
{
    /// <inheritdoc />
    public RoomMappingProfile()
    {
        CreateMap<DomainRoom, Room>()
            .ForMember(d => d.Access, opt => opt.Ignore());

        CreateMap<DomainRoomSettings, RoomSettings>();

        CreateMap<CreateRoomRequest, CreateRoom>()
            .ForMember(d => d.GameId, opt => opt.Ignore());

        CreateMap<Room, UpdateRoom>()
            .ForMember(d => d.RoomId, opt => opt.Ignore())
            .ForMember(d => d.AccessType, opt => opt.Ignore())
            .ForMember(d => d.ViewPrivateText, opt => opt.Ignore())
            .ForMember(d => d.ViewDiceResults, opt => opt.Ignore())
            .ForMember(d => d.DiceEnabled, opt => opt.Ignore())
            .ForMember(d => d.IsRemoved, opt => opt.Ignore());

        CreateMap<DomainRoomAccess, RoomAccess>()
            .ForMember(d => d.Character, s => s.MapFrom(a => a.Character))
            .ForMember(d => d.Policy, opt => opt.Ignore());

        CreateMap<RoomAccess, CreateRoomAccess>()
            .ForMember(d => d.CharacterId, s => s.MapFrom(r => r.Character != null ? r.Character.Id : (Guid?)null))
            .ForMember(d => d.ReaderUsername, s => s.MapFrom(r => r.User != null ? r.User.Username : null))
            .ForMember(d => d.RoomId, opt => opt.Ignore());

        CreateMap<RoomAccess, UpdateRoomAccess>()
            .ForMember(d => d.AccessId, s => s.MapFrom(r => r.Id));

        CreateMap<DomainPostPendency, PostPendency>()
            .ForMember(d => d.CreatedBy, s => s.MapFrom(p => p.CreatedBy))
            .ForMember(d => d.WaitingFor, s => s.MapFrom(p => p.WaitingForUser));

        CreateMap<PostPendency, DomainCreatePostPendency>()
            .ForMember(d => d.WaitingForUsername, s => s.MapFrom(p => p.WaitingFor != null ? p.WaitingFor.Username : null));
    }
}
