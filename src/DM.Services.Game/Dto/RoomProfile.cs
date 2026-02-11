using System.Linq;
using AutoMapper;
using DM.Services.Game.Dto.Internal;
using DM.Services.Game.Dto.Output;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;
using DbPostPendency = DM.Services.DataAccess.BusinessObjects.Games.Links.PostPendency;
using PostPendency = DM.Services.Game.Dto.Output.PostPendency;
using DbRoomAccess = DM.Services.DataAccess.BusinessObjects.Games.Links.RoomAccess;

namespace DM.Services.Game.Dto;

/// <inheritdoc />
internal class RoomProfile : Profile
{
    /// <inheritdoc />
    public RoomProfile()
    {
        CreateMap<DbRoom, RoomToUpdate>();
        CreateMap<DbRoom, Room>()
            .Include<DbRoom, RoomToUpdate>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.RoomId))
            .ForMember(d => d.Accesses, s => s.MapFrom(r => r.RoomAccesses))
            .ForMember(d => d.Pendencies, s => s.MapFrom(r => r.PostPendencies
                .Where(p =>
                    p.WaitingForUserId != null &&
                    (
                        p.Room.Game.MasterId == p.CreatedById ||
                        p.Room.Game.AssistantId == p.CreatedById ||
                        p.Room.RoomAccesses.Any(a => a.Character != null && a.Character.UserId == p.CreatedById)
                    ) &&
                    (
                        p.Room.Game.MasterId == p.WaitingForUserId ||
                        p.Room.Game.AssistantId == p.WaitingForUserId ||
                        p.Room.RoomAccesses.Any(a => a.Character != null && a.Character.UserId == p.WaitingForUserId)
                    ))))
            .ForMember(d => d.TotalPostsCount, s => s.MapFrom(r => r.Posts
                .Count(p => !p.IsRemoved)))
            .ForMember(d => d.Settings, s => s.MapFrom(r => new RoomSettings
            {
                ViewPrivateText = r.ViewPrivateText,
                ViewDiceResults = r.ViewDiceResults,
                DiceEnabled = r.DiceEnabled
            }));

        CreateMap<DbRoom, RoomOrderInfo>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.RoomId));

        CreateMap<DbRoomAccess, Output.RoomAccess>()
            .ForMember(d => d.Id, s => s.MapFrom(l => l.AccessId));
        CreateMap<DbPostPendency, PostPendency>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PendencyId))
            .ForMember(d => d.RoomId, s => s.MapFrom(p => p.RoomId))
            .ForMember(d => d.CharacterId, s => s.MapFrom(p => p.CharacterId))
            .ForMember(d => d.CreatedBy, s => s.MapFrom(p => p.CreatedBy))
            .ForMember(d => d.WaitingForUser, s => s.MapFrom(p => p.WaitingForUser))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(p => p.CreatedUtc))
            .ForMember(d => d.FulfilledUtc, s => s.MapFrom(p => p.FulfilledUtc));
    }
}