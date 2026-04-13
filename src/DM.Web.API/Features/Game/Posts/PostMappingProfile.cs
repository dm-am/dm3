using System;
using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Shared.BbRendering;
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
            .ForMember(d => d.Character, opt => opt.PreCondition(p => p.Character != null))
            // Explicit Room mapping. AutoMapper auto-discovers the nested
            // RoomRef → ApiRoom mapping by property name — but relying on
            // that convention alone is brittle: a future rename of either
            // side silently drops the navigation breadcrumb on rated-post
            // surfaces (home page "best post of the week", "latest featured
            // post") because they gate the breadcrumb on post.room.game.
            // Making the mapping explicit here is a regression guard.
            .ForMember(d => d.Room, opt => opt.MapFrom(s => s.Room))
            .AfterMap((src, dest) =>
            {
                // Populate the render-context envelope on both BbText fields
                // so the JSON converter can apply permission-aware filtering.
                var addresseeMap = ParsePrivateAddresseeSnapshot(src.PrivateAddresseeSnapshotJson);
                var envelope = new RenderContextEnvelope
                {
                    Surface = BbSurface.GamePost,
                    PostAuthorUserId = src.AuthorUserId,
                    PostId = src.Id,
                    GameId = src.GameId,
                    RoomId = src.RoomId,
                    PrivateAddresseeOwnerUserIdsByAttribute = addresseeMap,
                    GameLeadUserIds = src.GameLeadUserIds,
                    PostSharePrivateWithAll = src.SharePrivateWithAll,
                    RoomViewPrivateText = src.RoomViewPrivateText
                };
                if (dest.GameText is not null) dest.GameText.Context = envelope;
                if (dest.MetagameText is not null)
                    dest.MetagameText.Context = new RenderContextEnvelope
                    {
                        // MetagameText is OOC commentary — Comment surface
                        // (allows [mod] for moderators, never [private]).
                        Surface = BbSurface.Comment,
                        PostAuthorUserId = src.AuthorUserId,
                        PostId = src.Id,
                        GameId = src.GameId,
                        RoomId = src.RoomId
                    };
            });

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

        // Map RoomRef (domain) → Room (API) for rated post listings.
        // RoomRef.Game is a nested Domain Game object hydrated by
        // PostRepository.GetRated via the batched EnrichGamesAsync
        // helper; AutoMapper rewrites it through the existing
        // DtoGame → GameRef mapping so the API response carries the
        // full sidebar-tier game payload (master, assistants, active
        // characters, recruitment, subscribers) on every
        // post.room.game. Frontend components can render tooltips
        // directly off it without a second network round-trip — the
        // exact data-flow the sidebar's GameLink already uses.
        //
        // Fields that only exist on the dedicated /rooms endpoint
        // (access, accesses, pendencies, settings, unread counters)
        // stay ignored here — the rated-posts response deliberately
        // does not carry room-level administrative data.
        CreateMap<RoomRef, ApiRoom>()
            .ForMember(d => d.Game, opt => opt.MapFrom(s => s.Game))
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

    /// <summary>
    /// Parse the JSON snapshot produced at post save time. Shape:
    /// <c>{ "AddresseeAttrValue": ["guid", ...], ... }</c>. Bad JSON or
    /// an empty object yields an empty dictionary, which fails closed
    /// (only author / game leads / per-room override see [private]).
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlySet<Guid>> ParsePrivateAddresseeSnapshot(string? json)
    {
        var empty = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return empty;
            var result = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);
            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Array) continue;
                var set = new HashSet<Guid>();
                foreach (var item in entry.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String &&
                        Guid.TryParse(item.GetString(), out var id))
                        set.Add(id);
                }
                result[entry.Name] = set;
            }
            return result;
        }
        catch (JsonException)
        {
            return empty;
        }
    }
}
