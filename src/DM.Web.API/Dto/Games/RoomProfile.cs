using System;
using AutoMapper;
using DM.Services.Game.Dto.Input;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// Mapping profile for game models
/// </summary>
internal class RoomProfile : Profile
{
    /// <inheritdoc />
    public RoomProfile()
    {
        CreateMap<DM.Services.Game.Dto.Output.Room, Room>();
        CreateMap<DM.Services.Game.Dto.Output.RoomSettings, RoomSettings>();

        CreateMap<CreateRoomRequest, CreateRoom>();
        CreateMap<Room, UpdateRoom>();

        CreateMap<DM.Services.Game.Dto.Output.RoomAccess, RoomAccess>();

        CreateMap<RoomAccess, CreateRoomAccess>()
            .ForMember(d => d.CharacterId, s => s.MapFrom(r => r.Character != null ? r.Character.Id : (Guid?)null))
            .ForMember(d => d.ReaderLogin, s => s.MapFrom(r => r.User != null ? r.User.Login : null));
        CreateMap<RoomAccess, UpdateRoomAccess>()
            .ForMember(d => d.AccessId, s => s.MapFrom(r => r.Id));

        CreateMap<DM.Services.Game.Dto.Output.PostPendency, PostPendency>()
            .ForMember(d => d.CreatedBy, s => s.MapFrom(p => p.CreatedBy))
            .ForMember(d => d.WaitingFor, s => s.MapFrom(p => p.WaitingForUser));

        CreateMap<PostPendency, CreatePostPendency>()
            .ForMember(d => d.WaitingForUserLogin, s => s.MapFrom(p => p.WaitingFor != null ? p.WaitingFor.Login : null));
    }
}