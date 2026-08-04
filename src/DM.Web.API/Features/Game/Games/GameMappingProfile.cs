using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Web.API.Shared.BbRendering;
using DtoGame = DM.Domain.Game.Features.Games.Game;
using DtoGameDetails = DM.Domain.Game.Features.Games.GameDetails;
using DtoGameRecruitment = DM.Domain.Game.Features.Games.GameRecruitment;
using DtoCharacterShortInfo = DM.Domain.Game.Features.Games.CharacterShortInfo;
using DtoGamesQuery = DM.Domain.Game.Features.Games.GamesQuery;
using DtoGameTag = DM.Domain.Game.Features.Games.GameTag;
using DtoCreateGame = DM.Domain.Game.Features.Games.CreateGame;
using DtoUpdateGame = DM.Domain.Game.Features.Games.UpdateGame;
using DtoActiveCharacterInfo = DM.Domain.Game.Features.Games.ActiveCharacterInfo;
using DtoPlayerCharacterInfo = DM.Domain.Game.Features.Games.PlayerCharacterInfo;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Mapping profile for game models
/// </summary>
internal class GameMappingProfile : Profile
{
    /// <inheritdoc />
    public GameMappingProfile()
    {
        CreateMap<GamesQuery, DtoGamesQuery>()
            .ForMember(d => d.Statuses, o => o.MapFrom(s =>
                s.Statuses != null && s.Statuses.Any()
                    ? new HashSet<ModuleStatus>(s.Statuses)
                    : null))
            .ForMember(d => d.RequiredTags, o => o.MapFrom(s =>
                s.RequiredTags != null && s.RequiredTags.Any()
                    ? new HashSet<int>(s.RequiredTags)
                    : null))
            .ForMember(d => d.OptionalTags, o => o.MapFrom(s =>
                s.OptionalTags != null && s.OptionalTags.Any()
                    ? new HashSet<int>(s.OptionalTags)
                    : null))
            .ForMember(d => d.ExcludedTags, o => o.MapFrom(s =>
                s.ExcludedTags != null && s.ExcludedTags.Any()
                    ? new HashSet<int>(s.ExcludedTags)
                    : null))
            .ForMember(d => d.OwnerUsernames, o => o.MapFrom(s =>
                s.AuthorUsernames != null && s.AuthorUsernames.Any(u => !string.IsNullOrWhiteSpace(u))
                    ? new HashSet<string>(s.AuthorUsernames.Where(u => !string.IsNullOrWhiteSpace(u)))
                    : null))
            .ForMember(d => d.PremoderationStatuses, o => o.MapFrom(s =>
                s.PremoderationStatuses != null && s.PremoderationStatuses.Any()
                    ? new HashSet<PremoderationStatus>(s.PremoderationStatuses)
                    : null));
            // RecruitmentFilter, ClosedReasonFilter, PlayerUsername,
            // PlayerParticipation map by convention

        CreateMap<DtoGameRecruitment, GameRecruitment>();
        CreateMap<DtoActiveCharacterInfo, ActiveCharacterInfo>();
        CreateMap<DtoPlayerCharacterInfo, PlayerCharacterInfo>();

        // Note: GameAssistantInfo → UserRef mapping is in UserRefMappingProfile

        // GameRef mapping (lightweight, counts only - for sidebars/menus)
        // This is the BASE mapping that Game and GameDetails inherit from.
        // Tooltip-feeding collections (ActiveCharacters, SubscriberUsernames)
        // are EXPLICITLY mapped — relying on AutoMapper's convention walker
        // for nested IEnumerable properties across three-tier `IncludeBase`
        // chains is fragile (silent data loss when the submap is resolved
        // late or the element type check fails), and tooltips are part of
        // the critical UX surface that the user directly cares about.
        CreateMap<DtoGame, GameRef>()
            .ForMember(d => d.ActivatedUtc, s => s.MapFrom(g => g.ActivatedUtc))
            .ForMember(d => d.Participation, s => s.MapFrom<GameParticipationResolver>())
            .ForMember(d => d.Master, s => s.MapFrom(g => g.Master))
            .ForMember(d => d.Assistants, s => s.MapFrom(g => g.Assistants))
            .ForMember(d => d.SubscribersCount, s => s.MapFrom(g => g.SubscribersCount))
            .ForMember(d => d.ActiveCharacters, s => s.MapFrom(g => g.ActiveCharacters))
            .ForMember(d => d.SubscriberUsernames, s => s.MapFrom(g => g.SubscriberUsernames))
            .ForMember(d => d.Recruitment, s => s.MapFrom(g => g.Recruitment))
            .ForMember(d => d.GameReviewsCount, s => s.MapFrom(g => g.GameReviewsCount))
            .ForMember(d => d.PostReviewsCount, s => s.MapFrom(g => g.PostReviewsCount));

        // Game mapping (extends GameRef with additional fields)
        CreateMap<DtoGame, Game>()
            .IncludeBase<DtoGame, GameRef>()
            .ForMember(d => d.System, s => s.MapFrom(g => g.SystemName))
            .ForMember(d => d.Setting, s => s.MapFrom(g => g.NarrativeSetting))
            .ForMember(d => d.SchemaId, s => s.MapFrom(g => g.AttributeSchemaId))
            .ForMember(d => d.TagIds, s => s.MapFrom(g => g.TagIds))
            // Conditional expansion: hydrated only by the player-filtered
            // list path; PreCondition keeps null (instead of an empty
            // collection) so unfiltered responses omit the field entirely.
            .ForMember(d => d.PlayerCharacters, o =>
            {
                o.PreCondition(g => g.FilteredPlayerCharacters != null);
                o.MapFrom(g => g.FilteredPlayerCharacters);
            });

        // GameDetails mapping (extends Game with full details)
        CreateMap<DtoGameDetails, GameDetails>()
            .IncludeBase<DtoGame, Game>()
            .ForMember(d => d.PrivacySettings, s => s.MapFrom(g => g))
            .ForMember(d => d.Schema, s => s.MapFrom(g => g.AttributeSchema))
            .ForMember(d => d.FullAssistants, s => s.MapFrom(g => g.FullAssistants))
            .ForMember(d => d.Subscribers, s => s.MapFrom(g => g.Subscribers))
            // Readers ("Читатели") == game subscribers (GameRole.Reader is
            // subscription based); same source as Subscribers, roster-named.
            .ForMember(d => d.Readers, s => s.MapFrom(g => g.Subscribers))
            // Populate the render-context envelope on the public info so the
            // JSON converter honors the master's AuthorEdit round-trip (the
            // settings editor sends X-Dm-Audience: author_edit to load the raw
            // BBCode source) and downgrades any other viewer's author_edit
            // request to permission-filtered Display.
            .AfterMap((src, dest) =>
            {
                if (dest.Info is not null && src.Master is not null)
                {
                    dest.Info.Context = new RenderContextEnvelope
                    {
                        Surface = dest.Info.Surface,
                        PostAuthorUserId = src.Master.UserId
                    };
                }
            });

        CreateMap<DtoCharacterShortInfo, CharacterShortInfo>()
            .ForMember(d => d.Author, s => s.MapFrom(c => c.Author));

        CreateMap<DtoGameDetails, GamePrivacySettings>()
            .ForMember(d => d.ViewPrivates, s => s.MapFrom(g => g.ShowPrivateMessages))
            .ForMember(d => d.ViewDice, s => s.MapFrom(g => !g.HideDiceResult))
            .ForMember(d => d.ViewPostStats, s => s.MapFrom(g => !g.HidePostStats))
            .ForMember(d => d.CommentariesAccess, s => s.MapFrom(g => g.CommentsAccessMode));

        CreateMap<DtoGameTag, Tag>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.ShortId));

        // For game creation from API request
        CreateMap<CreateGameRequest, DtoCreateGame>()
            .ForMember(g => g.SystemName, s => s.MapFrom(r => r.System))
            .ForMember(g => g.NarrativeSetting, s => s.MapFrom(r => r.Setting))
            .ForMember(g => g.AttributeSchemaId, s => s.MapFrom(r => r.SchemaId))
            .ForMember(g => g.HideDiceResult, s => s.MapFrom(r => r.PrivacySettings != null && !r.PrivacySettings.ViewDice))
            .ForMember(g => g.ShowPrivateMessages, s => s.MapFrom(r => r.PrivacySettings != null && r.PrivacySettings.ViewPrivates))
            .ForMember(g => g.HidePostStats, s => s.MapFrom(r => r.PrivacySettings != null && !r.PrivacySettings.ViewPostStats))
            .ForMember(g => g.CommentsAccessMode, s => s.MapFrom(r => r.PrivacySettings != null ? r.PrivacySettings.CommentariesAccess : CommentsAccessMode.Public))
            // API exposes only the `Draft` bool, not draft visibility; the
            // enum defaults to Private server-side until the API surfaces it.
            .ForMember(g => g.DraftVisibility, opt => opt.Ignore())
            // Tags and AssistantUsername map by convention. An Ignore on either
            // is not a mapping detail: both are the master's input on the
            // creation form, and dropping them leaves a working control that
            // changes nothing and says nothing.
            .ForMember(g => g.CopyBlacklist, opt => opt.Ignore());

        // Request DTO in, write model out. The source used to be GameDetails —
        // the response — and every field of it that must not be writable needed
        // its own Ignore() below. Now the request names the editable fields and
        // nothing else, so a field added to the response cannot become writable
        // by being added.
        CreateMap<UpdateGameRequest, DtoUpdateGame>()
            .ForMember(g => g.SystemName, s => s.MapFrom(g => g.System))
            .ForMember(g => g.NarrativeSetting, s => s.MapFrom(g => g.Setting))
            .ForMember(g => g.AssistantUsername, opt => opt.Ignore())
            .ForMember(g => g.HideDiceResult, s => s.MapFrom(g => g.PrivacySettings != null ? !g.PrivacySettings.ViewDice : null))
            .ForMember(g => g.ShowPrivateMessages, s => s.MapFrom(g => g.PrivacySettings != null ? g.PrivacySettings.ViewPrivates : null))
            .ForMember(g => g.HidePostStats, s => s.MapFrom(g => g.PrivacySettings != null ? !g.PrivacySettings.ViewPostStats : null))
            .ForMember(g => g.CommentsAccessMode, s => s.MapFrom(g => g.PrivacySettings != null ? g.PrivacySettings.CommentariesAccess : null))
            .ForMember(g => g.IsRecruitmentOpen, s => s.MapFrom(g => g.Recruitment != null ? g.Recruitment.IsOpen : (bool?)null))
            .ForMember(g => g.RecruitmentPcLimit, s => s.MapFrom(g => g.Recruitment != null ? g.Recruitment.PcLimit : null))
            .ForMember(g => g.GameId, opt => opt.Ignore())
            // Draft visibility is not part of the update contract (API has no
            // such field); leave null so the domain keeps the current value.
            .ForMember(g => g.DraftVisibility, opt => opt.Ignore())
            .ForMember(g => g.MentorId, opt => opt.Ignore())
            .ForMember(g => g.RecruitmentStartedUtc, opt => opt.Ignore())
            .ForMember(g => g.IsRemoved, opt => opt.Ignore())
            .ForMember(g => g.Tags, opt => opt.Ignore());
    }
}
