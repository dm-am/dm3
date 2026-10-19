using DM.Domain.Game.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Features.Games;
using DM.Web.API.Features.Game.AttributeSchemas;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using DtoGame = DM.Domain.Game.Features.Games.Game;
using DtoGameDetails = DM.Domain.Game.Features.Games.GameDetails;
using DtoGameRecruitment = DM.Domain.Game.Features.Games.GameRecruitment;
using DtoActiveCharacterInfo = DM.Domain.Game.Features.Games.ActiveCharacterInfo;
using DtoPlayerCharacterInfo = DM.Domain.Game.Features.Games.PlayerCharacterInfo;
using DtoCharacterShortInfo = DM.Domain.Game.Features.Characters.CharacterShortInfo;
using DtoGameTag = DM.Domain.Game.Features.Games.GameTag;
using DtoGamesQuery = DM.Domain.Game.Features.Games.GamesQuery;
using DtoCreateGame = DM.Domain.Game.Features.Games.CreateGame;
using DtoUpdateGame = DM.Domain.Game.Features.Games.UpdateGame;
using DtoGameAttributeSchema = DM.Domain.Game.Features.GameAttributeSchemas.GameAttributeSchema;
using DtoReplaceGameAttributeSchema = DM.Domain.Game.Features.GameAttributeSchemas.ReplaceGameAttributeSchema;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Compile-time mapper for game models. The viewer's participation folds out of
/// the identity injected through the constructor.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(UserRefMappers))]
[UseStaticMapper(typeof(BbTextMappers))]
public partial class GameMapper
{
    [UseMapper]
    private readonly AttributeSchemaMapper _schemaMapper;

    private readonly IIdentityProvider _identityProvider;

    public GameMapper(AttributeSchemaMapper schemaMapper, IIdentityProvider identityProvider)
    {
        _schemaMapper = schemaMapper;
        _identityProvider = identityProvider;
    }

    /// <summary>
    /// Domain game to the lightweight sidebar/menu reference
    /// </summary>
    public GameRef ToGameRef(DtoGame game)
    {
        if (game == null)
        {
            return null!;
        }

        var result = ToGameRefCore(game);
        result.Participation = ParticipationOf(game);
        return result;
    }

    /// <summary>
    /// Domain game to the list/table DTO
    /// </summary>
    public Game ToGame(DtoGame game)
    {
        if (game == null)
        {
            return null!;
        }

        var result = ToGameCore(game);
        result.Participation = ParticipationOf(game);
        return result;
    }

    /// <summary>
    /// Domain game details to the full response DTO, with the master bound to the
    /// public info for the settings editor's AuthorEdit read.
    /// </summary>
    public GameDetails ToGameDetails(DtoGameDetails game)
    {
        if (game == null)
        {
            return null!;
        }

        var result = ToGameDetailsCore(game);
        result.Participation = ParticipationOf(game);
        result.PrivacySettings = new GamePrivacySettings
        {
            ViewPrivates = game.ShowPrivateMessages,
            ViewDice = !game.HideDiceResult,
            ViewPostStats = !game.HidePostStats,
            CommentariesAccess = game.CommentsAccessMode
        };
        // A reader of a game is its subscriber.
        result.Readers = result.Subscribers;
        result.ViewerBlacklisted = AnonymousIdentity.Of(_identityProvider.Current?.User) is { } viewerId &&
                                   game.IsBlacklisted(viewerId);
        if (game.Master is not null)
        {
            result.Info?.BindAuthor(game.Master.UserId,
                intentions => intentions.IsAllowed(GameIntention.EditSettings, game));
        }

        return result;
    }

    /// <summary>
    /// API filter query to the domain one. The five id/status sets fold to
    /// null when absent or empty - the domain reads null as "no filter", and
    /// an empty set must not turn into "match nothing".
    /// </summary>
    public DtoGamesQuery ToGamesQuery(GamesQuery query)
    {
        var result = ToGamesQueryCore(query);
        result.Statuses = ToSetOrNull(query.Statuses);
        result.RequiredTags = ToSetOrNull(query.RequiredTags);
        result.OptionalTags = ToSetOrNull(query.OptionalTags);
        result.ExcludedTags = ToSetOrNull(query.ExcludedTags);
        result.OwnerUsernames = ToSetOrNull(query.HostUsernames?.Where(u => !string.IsNullOrWhiteSpace(u)));
        result.PremoderationStatuses = ToSetOrNull(query.PremoderationStatuses);
        return result;
    }

    private static HashSet<T>? ToSetOrNull<T>(IEnumerable<T>? values) =>
        values != null && values.Any() ? new HashSet<T>(values) : null;

    /// <summary>
    /// Domain tag to the API one - the public identifier is the short alias
    /// the tag list and the game filters speak.
    /// </summary>
    [MapProperty(nameof(DtoGameTag.ShortId), nameof(Tag.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial Tag ToTag(DtoGameTag tag);

    /// <summary>
    /// Create request to the write model
    /// </summary>
    public DtoCreateGame ToCreateGame(CreateGameRequest request)
    {
        var result = ToCreateGameCore(request);
        result.HideDiceResult = request.PrivacySettings != null && !request.PrivacySettings.ViewDice;
        result.ShowPrivateMessages = request.PrivacySettings != null && request.PrivacySettings.ViewPrivates;
        result.HidePostStats = request.PrivacySettings != null && !request.PrivacySettings.ViewPostStats;
        result.CommentsAccessMode = request.PrivacySettings?.CommentariesAccess ?? CommentsAccessMode.Public;
        return result;
    }

    /// <summary>
    /// Update request to the write model. GameId comes from the route, the
    /// service sets it. An omitted privacy or recruitment block must stay
    /// absent: every destination flag is nullable on purpose, and null means
    /// "leave the stored value alone".
    /// </summary>
    public DtoUpdateGame ToUpdateGame(UpdateGameRequest request)
    {
        var result = ToUpdateGameCore(request);
        // An absent privacy block folds to null, not to a value.
        result.HideDiceResult = request.PrivacySettings == null ? null : !request.PrivacySettings.ViewDice;
        result.ShowPrivateMessages = request.PrivacySettings?.ViewPrivates;
        result.HidePostStats = request.PrivacySettings == null ? null : !request.PrivacySettings.ViewPostStats;
        result.CommentsAccessMode = request.PrivacySettings?.CommentariesAccess;
        result.IsRecruitmentOpen = request.Recruitment?.IsOpen;
        result.RecruitmentPcLimit = request.Recruitment?.PcLimit;
        return result;
    }

    /// <summary>
    /// Current user participation flags, flattened for the client. Reader comes
    /// from the repository, which read the game for the same current user.
    /// </summary>
    public IEnumerable<GameParticipation> ParticipationOf(DtoGame game)
    {
        // Not the empty Guid: an unfilled master id would answer Owner to a guest.
        var userId = AnonymousIdentity.Of(_identityProvider.Current?.User);
        var participation = GameParticipation.None;

        if (userId is { } readerId)
        {
            if (game.Master?.UserId == readerId)
            {
                participation |= GameParticipation.Owner | GameParticipation.Authority;
            }

            if (game.Assistants?.Any(a => a.UserId == readerId) == true)
            {
                participation |= GameParticipation.Authority;
            }

            if (game.PendingAssistant?.UserId == readerId)
            {
                participation |= GameParticipation.PendingAssistant;
            }

            if (game.Players?.Any(p => p.UserId == readerId) == true)
            {
                participation |= GameParticipation.Player;
            }

            if (game.Mentor?.UserId == readerId)
            {
                participation |= GameParticipation.Moderator;
            }
        }

        if (game.IsViewerSubscriber)
        {
            participation |= GameParticipation.Reader;
        }

        return Enum.GetValues<GameParticipation>()
            .Where(p => p != GameParticipation.None && (p & participation) == p);
    }

    [MapperIgnoreTarget(nameof(GameRef.Participation))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial GameRef ToGameRefCore(DtoGame game);

    [MapperIgnoreTarget(nameof(GameRef.Participation))]
    [MapProperty(nameof(DtoGame.SystemName), nameof(Game.System))]
    [MapProperty(nameof(DtoGame.NarrativeSetting), nameof(Game.Setting))]
    // Only the player-filtered list hydrates it; null keeps it out of other responses.
    [MapProperty(nameof(DtoGame.FilteredPlayerCharacters), nameof(Game.PlayerCharacters))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Game ToGameCore(DtoGame game);

    // Composed in ToGameDetails, the viewer in GameApiService.
    [MapperIgnoreTarget(nameof(GameRef.Participation))]
    [MapperIgnoreTarget(nameof(GameDetails.PrivacySettings))]
    [MapperIgnoreTarget(nameof(GameDetails.Readers))]
    [MapperIgnoreTarget(nameof(GameDetails.ViewerBlacklisted))]
    [MapperIgnoreTarget(nameof(GameDetails.Viewer))]
    [MapProperty(nameof(DtoGame.SystemName), nameof(Game.System))]
    [MapProperty(nameof(DtoGame.NarrativeSetting), nameof(Game.Setting))]
    [MapProperty(nameof(DtoGameDetails.AttributeSchema), nameof(GameDetails.Schema))]
    [MapProperty(nameof(DtoGame.FilteredPlayerCharacters), nameof(Game.PlayerCharacters))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial GameDetails ToGameDetailsCore(DtoGameDetails game);

    /// <summary>
    /// Domain sheet to its response DTO
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial GameAttributeSchema ToGameAttributeSchema(DtoGameAttributeSchema schema);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial GameRecruitment ToGameRecruitment(DtoGameRecruitment recruitment);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial ActiveCharacterInfo ToActiveCharacterInfo(DtoActiveCharacterInfo info);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial PlayerCharacterInfo ToPlayerCharacterInfo(DtoPlayerCharacterInfo info);

    // An NPC has no author, nullable on both sides.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial CharacterShortInfo ToCharacterShortInfo(DtoCharacterShortInfo info);

    // The set filters fold in ToGamesQuery.
    [MapperIgnoreTarget(nameof(DtoGamesQuery.Statuses))]
    [MapperIgnoreTarget(nameof(DtoGamesQuery.RequiredTags))]
    [MapperIgnoreTarget(nameof(DtoGamesQuery.OptionalTags))]
    [MapperIgnoreTarget(nameof(DtoGamesQuery.ExcludedTags))]
    [MapperIgnoreTarget(nameof(DtoGamesQuery.OwnerUsernames))]
    [MapperIgnoreTarget(nameof(DtoGamesQuery.PremoderationStatuses))]
    // Filled by the service from the viewer's blacklist, never by the caller.
    [MapperIgnoreTarget(nameof(DtoGamesQuery.ExcludedMasterIds))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial DtoGamesQuery ToGamesQueryCore(GamesQuery query);

    [MapperIgnoreTarget(nameof(DtoCreateGame.HideDiceResult))]
    [MapperIgnoreTarget(nameof(DtoCreateGame.ShowPrivateMessages))]
    [MapperIgnoreTarget(nameof(DtoCreateGame.HidePostStats))]
    [MapperIgnoreTarget(nameof(DtoCreateGame.CommentsAccessMode))]
    // The API has no draft visibility; the domain default is Private.
    [MapperIgnoreTarget(nameof(DtoCreateGame.DraftVisibility))]
    [MapProperty(nameof(CreateGameRequest.System), nameof(DtoCreateGame.SystemName))]
    [MapProperty(nameof(CreateGameRequest.Setting), nameof(DtoCreateGame.NarrativeSetting))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial DtoCreateGame ToCreateGameCore(CreateGameRequest request);

    /// <summary>
    /// Replacement request to the domain command. The game comes from the
    /// route, the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DtoReplaceGameAttributeSchema.GameId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DtoReplaceGameAttributeSchema ToReplaceGameAttributeSchema(
        ReplaceGameAttributeSchemaRequest request);

    [MapperIgnoreTarget(nameof(DtoUpdateGame.GameId))]
    [MapperIgnoreTarget(nameof(DtoUpdateGame.HideDiceResult))]
    [MapperIgnoreTarget(nameof(DtoUpdateGame.ShowPrivateMessages))]
    [MapperIgnoreTarget(nameof(DtoUpdateGame.HidePostStats))]
    [MapperIgnoreTarget(nameof(DtoUpdateGame.CommentsAccessMode))]
    [MapperIgnoreTarget(nameof(DtoUpdateGame.IsRecruitmentOpen))]
    [MapperIgnoreTarget(nameof(DtoUpdateGame.RecruitmentPcLimit))]
    [MapProperty(nameof(UpdateGameRequest.System), nameof(DtoUpdateGame.SystemName))]
    [MapProperty(nameof(UpdateGameRequest.Setting), nameof(DtoUpdateGame.NarrativeSetting))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial DtoUpdateGame ToUpdateGameCore(UpdateGameRequest request);
}
