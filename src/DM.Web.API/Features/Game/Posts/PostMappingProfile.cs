using System;
using AutoMapper;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Web.API.Features.Game.Games;
using ApiRoom = DM.Web.API.Features.Game.Rooms.Room;
using DomainPost = DM.Domain.Game.Features.Games.Post;
using DomainPostEdit = DM.Domain.Game.Features.Posts.PostEdit;
using DomainDiceRoll = DM.Domain.Game.Features.Posts.DiceRoll;
using DomainDiceRollResult = DM.Domain.Game.Features.Posts.DiceRollResult;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// Mapping profile for game post models
/// </summary>
internal class PostMappingProfile : Profile
{
    /// <inheritdoc />
    public PostMappingProfile()
    {
        CreateMap<DomainPost, Post>()
            // Character can be null for posts without character
            .ForMember(d => d.Character, opt => opt.PreCondition(p => p.Character != null));

        // PostEdit mapping
        CreateMap<DomainPostEdit, PostEditInfo>();

        // DiceRoll mapping
        CreateMap<DomainDiceRoll, DiceRoll>()
            .ForMember(d => d.Rolls, opt => opt.MapFrom(s => s.DiceCount))
            .ForMember(d => d.Edges, opt => opt.MapFrom(s => s.EdgesCount))
            .ForMember(d => d.Explosion, opt => opt.MapFrom(s => s.ExplosionCount))
            .ForMember(d => d.Results, opt => opt.MapFrom(s => s.Results));

        CreateMap<DomainDiceRollResult, DiceResult>()
            .ForMember(d => d.Critical, opt => opt.MapFrom(s => s.IsCritical))
            .ForMember(d => d.Exploded, opt => opt.MapFrom(s => s.IsExploded));

        // Map RoomRef to Room (for rated posts with navigation info)
        CreateMap<RoomRef, ApiRoom>()
            .ForMember(d => d.Game, opt => opt.MapFrom(s => s.GameId != Guid.Empty ? new GameRef
            {
                Id = s.GameId,
                PublicId = s.GamePublicId,
                Title = s.GameTitle
            } : null))
            .ForMember(d => d.PreviousRoomId, opt => opt.Ignore())
            .ForMember(d => d.Access, opt => opt.Ignore())
            .ForMember(d => d.Type, opt => opt.Ignore())
            .ForMember(d => d.Accesses, opt => opt.Ignore())
            .ForMember(d => d.Pendencies, opt => opt.Ignore())
            .ForMember(d => d.UnreadPostsCount, opt => opt.Ignore())
            .ForMember(d => d.Settings, opt => opt.Ignore());

        CreateMap<CreatePostRequest, CreatePost>()
            .ForMember(d => d.RoomId, opt => opt.Ignore());

        CreateMap<Post, UpdatePost>()
            .ForMember(d => d.PostId, opt => opt.Ignore())
            .ForMember(d => d.IsRemoved, opt => opt.Ignore());
    }
}
