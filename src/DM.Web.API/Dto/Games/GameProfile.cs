using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using DM.Services.Core.Dto.Enums;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;
using DtoGame = DM.Services.Gaming.Dto.Output.Game;
using DtoGameExtended = DM.Services.Gaming.Dto.Output.GameExtended;
using DtoGameRecruitment = DM.Services.Gaming.Dto.Output.GameRecruitment;
using DtoCharacterShortInfo = DM.Services.Gaming.Dto.Output.CharacterShortInfo;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// Mapping profile for game models
/// </summary>
internal class GameProfile : Profile
{
    /// <inheritdoc />
    public GameProfile()
    {
        CreateMap<GamesQuery, DM.Services.Gaming.Dto.Input.GamesQuery>()
            .ForMember(d => d.Statuses, o => o.MapFrom(s =>
                s.Statuses != null && s.Statuses.Any()
                    ? new HashSet<GameStatus>(s.Statuses)
                    : new HashSet<GameStatus> { GameStatus.Active }));

        CreateMap<DtoGameRecruitment, GameRecruitment>();

        // Base Game mapping (lightweight)
        CreateMap<DtoGame, Game>()
            .ForMember(d => d.System, s => s.MapFrom(g => g.SystemName))
            .ForMember(d => d.Setting, s => s.MapFrom(g => g.NarrativeSetting))
            .ForMember(d => d.SchemaId, s => s.MapFrom(g => g.AttributeSchemaId))
            .ForMember(d => d.Released, s => s.MapFrom(g => g.ReleaseDate ?? g.CreatedUtc))
            .ForMember(d => d.Participation, s => s.MapFrom<GameParticipationResolver>());

        // GameDetails mapping (full, inherits from Game)
        CreateMap<DtoGameExtended, GameDetails>()
            .ForMember(d => d.System, s => s.MapFrom(g => g.SystemName))
            .ForMember(d => d.Setting, s => s.MapFrom(g => g.NarrativeSetting))
            .ForMember(d => d.SchemaId, s => s.MapFrom(g => g.AttributeSchemaId))
            .ForMember(d => d.Released, s => s.MapFrom(g => g.ReleaseDate ?? g.CreatedUtc))
            .ForMember(d => d.Participation, s => s.MapFrom<GameParticipationResolver>())
            .ForMember(d => d.PrivacySettings, s => s.MapFrom(g => g))
            .ForMember(d => d.Schema, s => s.MapFrom(g => g.AttributeSchema));

        CreateMap<DtoCharacterShortInfo, CharacterShortInfo>()
            .ForMember(d => d.Author, s => s.MapFrom(c => c.Author));

        CreateMap<DtoGameExtended, GamePrivacySettings>()
            .ForMember(d => d.ViewTemper, s => s.MapFrom(g => !g.HideTemper))
            .ForMember(d => d.ViewStory, s => s.MapFrom(g => !g.HideStory))
            .ForMember(d => d.ViewSkills, s => s.MapFrom(g => !g.HideSkills))
            .ForMember(d => d.ViewInventory, s => s.MapFrom(g => !g.HideInventory))
            .ForMember(d => d.ViewPrivates, s => s.MapFrom(g => g.ShowPrivateMessages))
            .ForMember(d => d.ViewDice, s => s.MapFrom(g => !g.HideDiceResult))
            .ForMember(d => d.CommentariesAccess, s => s.MapFrom(g => g.CommentariesAccessMode));

        CreateMap<GameTag, Tag>()
            .ForMember(d => d.Category, s => s.MapFrom(t => t.GroupTitle))
            .ReverseMap();

        // For game creation, use GameDetails (has PrivacySettings)
        CreateMap<GameDetails, CreateGame>()
            .ForMember(g => g.SystemName, s => s.MapFrom(g => g.System))
            .ForMember(g => g.NarrativeSetting, s => s.MapFrom(g => g.Setting))
            .ForMember(g => g.AttributeSchemaId, s => s.MapFrom(g => g.Schema != null ? g.Schema.Id : g.SchemaId))
            .ForMember(g => g.AssistantLogin, s => s.MapFrom(g => g.Assistant != null ? g.Assistant.Login : null))
            .ForMember(g => g.Draft, s => s.MapFrom(g => g.Status == GameStatus.Draft))
            .ForMember(g => g.HideTemper, s => s.MapFrom(g => g.PrivacySettings != null && !g.PrivacySettings.ViewTemper.GetValueOrDefault(true)))
            .ForMember(g => g.HideStory, s => s.MapFrom(g => g.PrivacySettings != null && !g.PrivacySettings.ViewStory.GetValueOrDefault(true)))
            .ForMember(g => g.HideSkills, s => s.MapFrom(g => g.PrivacySettings != null && !g.PrivacySettings.ViewSkills.GetValueOrDefault(true)))
            .ForMember(g => g.HideInventory, s => s.MapFrom(g => g.PrivacySettings != null && !g.PrivacySettings.ViewInventory.GetValueOrDefault(true)))
            .ForMember(g => g.HideDiceResult, s => s.MapFrom(g => g.PrivacySettings != null && !g.PrivacySettings.ViewDice.GetValueOrDefault(true)))
            .ForMember(g => g.ShowPrivateMessages, s => s.MapFrom(g => g.PrivacySettings != null && g.PrivacySettings.ViewPrivates.GetValueOrDefault(false)))
            .ForMember(g => g.CommentariesAccessMode, s => s.MapFrom(g => g.PrivacySettings != null ? g.PrivacySettings.CommentariesAccess : null))
            .ForMember(g => g.Tags, s => s.MapFrom(g => g.Tags != null ? g.Tags.Select(t => t.Id) : null));

        // For game update, use GameDetails (has PrivacySettings)
        CreateMap<GameDetails, UpdateGame>()
            .ForMember(g => g.SystemName, s => s.MapFrom(g => g.System))
            .ForMember(g => g.NarrativeSetting, s => s.MapFrom(g => g.Setting))
            .ForMember(g => g.AssistantLogin, s => s.MapFrom(g => g.Assistant != null ? g.Assistant.Login : null))
            .ForMember(g => g.HideTemper, s => s.MapFrom(g => g.PrivacySettings != null ? !g.PrivacySettings.ViewTemper : null))
            .ForMember(g => g.HideStory, s => s.MapFrom(g => g.PrivacySettings != null ? !g.PrivacySettings.ViewStory : null))
            .ForMember(g => g.HideSkills, s => s.MapFrom(g => g.PrivacySettings != null ? !g.PrivacySettings.ViewSkills : null))
            .ForMember(g => g.HideInventory, s => s.MapFrom(g => g.PrivacySettings != null ? !g.PrivacySettings.ViewInventory : null))
            .ForMember(g => g.HideDiceResult, s => s.MapFrom(g => g.PrivacySettings != null ? !g.PrivacySettings.ViewDice : null))
            .ForMember(g => g.ShowPrivateMessages, s => s.MapFrom(g => g.PrivacySettings != null ? g.PrivacySettings.ViewPrivates : null))
            .ForMember(g => g.CommentariesAccessMode, s => s.MapFrom(g => g.PrivacySettings != null ? g.PrivacySettings.CommentariesAccess : null))
            .ForMember(g => g.IsRecruitmentOpen, s => s.MapFrom(g => g.Recruitment != null ? g.Recruitment.IsOpen : (bool?)null))
            .ForMember(g => g.RecruitmentPlayerLimit, s => s.MapFrom(g => g.Recruitment != null ? g.Recruitment.PlayerLimit : null));
    }
}