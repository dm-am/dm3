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
            // Project Domain.Room.AccessType → Api.Room.Access. Previously
            // ignored, which left every room in game-details responses
            // with `access = null`. Downstream tooltip logic then defaulted
            // every room to "open" and leaked the wrong character list.
            .ForMember(d => d.Access, s => s.MapFrom(r => r.AccessType))
            // Rooms are returned nested under their game; the domain Room
            // carries only GameId (no GameRef nav), so this back-reference
            // stays null instead of being re-hydrated per room.
            .ForMember(d => d.Game, opt => opt.Ignore());

        CreateMap<DomainRoomSettings, RoomSettings>();

        CreateMap<CreateRoomRequest, CreateRoom>()
            .ForMember(d => d.GameId, opt => opt.Ignore());

        CreateMap<UpdateRoomRequest, UpdateRoom>()
            .ForMember(d => d.RoomId, opt => opt.Ignore())
            // ChatId is server-managed (linked chat room); the API Room DTO
            // does not expose it, so an update never reassigns it.
            .ForMember(d => d.ChatId, opt => opt.Ignore())
            // The settings UI edits these; the repository applies each only when
            // its nullable is set. AccessType comes from the flat Access field,
            // the settings toggles from the nested Settings block — a sparse
            // PATCH with Settings omitted leaves them null (== unchanged).
            .ForMember(d => d.AccessType, s => s.MapFrom(r => r.Access))
            .ForMember(d => d.ViewPrivateText,
                s => s.MapFrom(r => r.Settings != null ? (bool?)r.Settings.ViewPrivateText : null))
            .ForMember(d => d.ViewDiceResults,
                s => s.MapFrom(r => r.Settings != null ? (bool?)r.Settings.ViewDiceResults : null))
            .ForMember(d => d.DiceEnabled,
                s => s.MapFrom(r => r.Settings != null ? (bool?)r.Settings.DiceEnabled : null))
            .ForMember(d => d.HiddenWithoutAccess,
                s => s.MapFrom(r => r.Settings != null ? (bool?)r.Settings.HiddenWithoutAccess : null))
            .ForMember(d => d.IsRemoved, opt => opt.Ignore());

        // Policy used to be ignored here, so every access of every response
        // answered null: the screen that grants access could not show which grant
        // it had just made, and the same value now decides who may write.
        CreateMap<DomainRoomAccess, RoomAccess>()
            .ForMember(d => d.Character, s => s.MapFrom(a => a.Character));

        CreateMap<RoomAccess, CreateRoomAccess>()
            .ForMember(d => d.CharacterId, s => s.MapFrom(r => r.Character != null ? r.Character.Id : (Guid?)null))
            .ForMember(d => d.ReaderUsername, s => s.MapFrom(r => r.User != null ? r.User.Username : null))
            .ForMember(d => d.RoomId, opt => opt.Ignore());

        CreateMap<UpdateRoomAccessRequest, UpdateRoomAccess>()
            .ForMember(d => d.AccessId, opt => opt.Ignore());

        CreateMap<DomainPostPendency, PostPendency>()
            .ForMember(d => d.CreatedBy, s => s.MapFrom(p => p.CreatedBy))
            .ForMember(d => d.WaitingFor, s => s.MapFrom(p => p.WaitingForUser));

        CreateMap<PostPendency, DomainCreatePostPendency>()
            .ForMember(d => d.WaitingForUsername, s => s.MapFrom(p => p.WaitingFor != null ? p.WaitingFor.Username : null));
    }
}
