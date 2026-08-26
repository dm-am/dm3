using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Web.API.Features.Game.AttributeSchemas;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using DtoGame = DM.Domain.Game.Features.Games.Game;
using DtoGameDetails = DM.Domain.Game.Features.Games.GameDetails;
using DtoGameRecruitment = DM.Domain.Game.Features.Games.GameRecruitment;
using DtoActiveCharacterInfo = DM.Domain.Game.Features.Games.ActiveCharacterInfo;
using DtoPlayerCharacterInfo = DM.Domain.Game.Features.Games.PlayerCharacterInfo;
using DtoCharacterShortInfo = DM.Domain.Game.Features.Games.CharacterShortInfo;
using DtoGameTag = DM.Domain.Game.Features.Games.GameTag;
using DtoGamesQuery = DM.Domain.Game.Features.Games.GamesQuery;
using DtoCreateGame = DM.Domain.Game.Features.Games.CreateGame;
using DtoUpdateGame = DM.Domain.Game.Features.Games.UpdateGame;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Compile-time mapper for game models. The viewer's participation folds out
/// of the identity injected through the constructor instead of the
/// GameParticipationResolver indirection, and the render-context envelope on
/// the public info is an explicit step of <see cref="ToGameDetails"/>.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(UserRefMappers))]
[UseStaticMapper(typeof(BbTextMappers))]
internal partial class GameMapper
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
    /// Domain game details to the full response DTO. The explicit envelope
    /// step replaces the AutoMapper AfterMap: the master owns the public
    /// info, so the envelope is what lets the JSON converter honor the
    /// master's AuthorEdit round-trip (the settings editor sends
    /// X-Dm-Audience: author_edit to load the raw BBCode source) while
    /// downgrading any other viewer's author_edit request to
    /// permission-filtered Display.
    /// </summary>
    public GameDetails ToGameDetails(DtoGameDetails game)
    {
        if (game == null)
        {
            return null!;
        }

        var result = ToGameDetailsCore(game);
        result.Participation = ParticipationOf(game);
        // The schema member is `= null!` on the domain side yet null for
        // every game without one; the guard keeps null flowing through.
        result.Schema = game.AttributeSchema == null ? null : _schemaMapper.ToSchema(game.AttributeSchema);
        result.PrivacySettings = new GamePrivacySettings
        {
            ViewPrivates = game.ShowPrivateMessages,
            ViewDice = !game.HideDiceResult,
            ViewPostStats = !game.HidePostStats,
            CommentariesAccess = game.CommentsAccessMode
        };
        // Readers ("Читатели") == game subscribers (GameRole.Reader is
        // subscription based); same source, roster-named.
        result.Readers = result.Subscribers;
        if (result.Info is not null && game.Master is not null)
        {
            result.Info.Context = new RenderContextEnvelope
            {
                Surface = result.Info.Surface,
                PostAuthorUserId = game.Master.UserId
            };
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
        result.Statuses = query.Statuses != null && query.Statuses.Any()
            ? new HashSet<ModuleStatus>(query.Statuses)
            : null;
        result.RequiredTags = query.RequiredTags != null && query.RequiredTags.Any()
            ? new HashSet<int>(query.RequiredTags)
            : null;
        result.OptionalTags = query.OptionalTags != null && query.OptionalTags.Any()
            ? new HashSet<int>(query.OptionalTags)
            : null;
        result.ExcludedTags = query.ExcludedTags != null && query.ExcludedTags.Any()
            ? new HashSet<int>(query.ExcludedTags)
            : null;
        result.OwnerUsernames = query.HostUsernames != null && query.HostUsernames.Any(u => !string.IsNullOrWhiteSpace(u))
            ? new HashSet<string>(query.HostUsernames.Where(u => !string.IsNullOrWhiteSpace(u)))
            : null;
        result.PremoderationStatuses = query.PremoderationStatuses != null && query.PremoderationStatuses.Any()
            ? new HashSet<PremoderationStatus>(query.PremoderationStatuses)
            : null;
        return result;
    }

    /// <summary>
    /// Domain tag to the API one - the public identifier is the short alias
    /// the tag list and the game filters speak.
    /// </summary>
    [MapProperty(nameof(DtoGameTag.ShortId), nameof(Tag.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial Tag ToTag(DtoGameTag tag);

    /// <summary>
    /// Create request to the write model. Draft visibility is not part of
    /// the API contract yet (the enum defaults to Private server-side), and
    /// the blacklist copy switch is not surfaced either.
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
        // Lifted negation on purpose: a privacy block whose flag was not
        // sent folds to null, not to a value.
        result.HideDiceResult = request.PrivacySettings == null ? null : !request.PrivacySettings.ViewDice;
        result.ShowPrivateMessages = request.PrivacySettings?.ViewPrivates;
        result.HidePostStats = request.PrivacySettings == null ? null : !request.PrivacySettings.ViewPostStats;
        result.CommentsAccessMode = request.PrivacySettings?.CommentariesAccess;
        result.IsRecruitmentOpen = request.Recruitment?.IsOpen;
        result.RecruitmentPcLimit = request.Recruitment?.PcLimit;
        return result;
    }

    /// <summary>
    /// Current user participation flags, flattened for the client. The one
    /// bit the repository resolved rather than this method is Reader: the
    /// flag is filled for the user the game was read for, and that is the
    /// same current user this mapper asks about.
    /// </summary>
    public IEnumerable<GameParticipation> ParticipationOf(DtoGame game)
    {
        // Through AnonymousIdentity rather than the empty Guid. The empty id is
        // the absence of an identity, and it is also what an unfilled field holds,
        // so comparing by it answers "yes, this is you" to a visitor who is nobody
        // against a game whose master the projection did not fill - and Owner here
        // is what the client draws the settings and the master's controls from.
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
        }

        if (game.IsViewerSubscriber)
        {
            participation |= GameParticipation.Reader;
        }

        if (game.Mentor?.UserId == userId)
        {
            participation |= GameParticipation.Moderator;
        }

        return Enum.GetValues<GameParticipation>()
            .Where(p => p != GameParticipation.None && (p & participation) == p);
    }

    // A narrowing projection by design: the domain game carries the roster
    // and moderation fields the sidebar reference never answers.
    [MapperIgnoreTarget(nameof(GameRef.Participation))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial GameRef ToGameRefCore(DtoGame game);

    [MapperIgnoreTarget(nameof(GameRef.Participation))]
    [MapProperty(nameof(DtoGame.SystemName), nameof(Game.System))]
    [MapProperty(nameof(DtoGame.NarrativeSetting), nameof(Game.Setting))]
    [MapProperty(nameof(DtoGame.AttributeSchemaId), nameof(Game.SchemaId))]
    // Conditional expansion: hydrated only by the player-filtered list path;
    // a null source keeps null (instead of an empty collection) so
    // unfiltered responses omit the field entirely.
    [MapProperty(nameof(DtoGame.FilteredPlayerCharacters), nameof(Game.PlayerCharacters))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Game ToGameCore(DtoGame game);

    // Participation, privacy fold, schema and the info envelope live in the
    // wrapper; Readers shares the Subscribers projection there too.
    [MapperIgnoreTarget(nameof(GameRef.Participation))]
    [MapperIgnoreTarget(nameof(GameDetails.PrivacySettings))]
    [MapperIgnoreTarget(nameof(GameDetails.Schema))]
    [MapperIgnoreTarget(nameof(GameDetails.Readers))]
    [MapProperty(nameof(DtoGame.SystemName), nameof(Game.System))]
    [MapProperty(nameof(DtoGame.NarrativeSetting), nameof(Game.Setting))]
    [MapProperty(nameof(DtoGame.AttributeSchemaId), nameof(Game.SchemaId))]
    [MapProperty(nameof(DtoGame.FilteredPlayerCharacters), nameof(Game.PlayerCharacters))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial GameDetails ToGameDetailsCore(DtoGameDetails game);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial GameRecruitment ToGameRecruitment(DtoGameRecruitment recruitment);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial ActiveCharacterInfo ToActiveCharacterInfo(DtoActiveCharacterInfo info);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial PlayerCharacterInfo ToPlayerCharacterInfo(DtoPlayerCharacterInfo info);

    // An NPC has no author, and both sides say so: the domain member is
    // nullable and the roster line answers with no owner rather than with a
    // user nobody can name.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial CharacterShortInfo ToCharacterShortInfo(DtoCharacterShortInfo info);

    // The set filters fold in the wrapper - null and empty both mean "no
    // filter" and the domain speaks HashSet.
    [MapperIgnoreTarget(nameof(DtoGamesQuery.Statuses))]
    [MapperIgnoreTarget(nameof(DtoGamesQuery.RequiredTags))]
    [MapperIgnoreTarget(nameof(DtoGamesQuery.OptionalTags))]
    [MapperIgnoreTarget(nameof(DtoGamesQuery.ExcludedTags))]
    [MapperIgnoreTarget(nameof(DtoGamesQuery.OwnerUsernames))]
    [MapperIgnoreTarget(nameof(DtoGamesQuery.PremoderationStatuses))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial DtoGamesQuery ToGamesQueryCore(GamesQuery query);

    [MapperIgnoreTarget(nameof(DtoCreateGame.HideDiceResult))]
    [MapperIgnoreTarget(nameof(DtoCreateGame.ShowPrivateMessages))]
    [MapperIgnoreTarget(nameof(DtoCreateGame.HidePostStats))]
    [MapperIgnoreTarget(nameof(DtoCreateGame.CommentsAccessMode))]
    // API exposes only the `Draft` bool, not draft visibility; the enum
    // defaults to Private server-side until the API surfaces it. The
    // blacklist copy switch is not surfaced either - an Ignore here is not a
    // mapping detail, see the creation form notes.
    [MapperIgnoreTarget(nameof(DtoCreateGame.DraftVisibility))]
    [MapperIgnoreTarget(nameof(DtoCreateGame.CopyBlacklist))]
    [MapProperty(nameof(CreateGameRequest.System), nameof(DtoCreateGame.SystemName))]
    [MapProperty(nameof(CreateGameRequest.Setting), nameof(DtoCreateGame.NarrativeSetting))]
    [MapProperty(nameof(CreateGameRequest.SchemaId), nameof(DtoCreateGame.AttributeSchemaId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial DtoCreateGame ToCreateGameCore(CreateGameRequest request);

    // Tags map by convention: both sides are the short identifiers the tag
    // list serves, and both spell "leave them alone" as null.
    [MapperIgnoreTarget(nameof(DtoUpdateGame.GameId))]
    [MapperIgnoreTarget(nameof(DtoUpdateGame.DraftVisibility))]
    [MapperIgnoreTarget(nameof(DtoUpdateGame.MentorId))]
    [MapperIgnoreTarget(nameof(DtoUpdateGame.RecruitmentStartedUtc))]
    [MapperIgnoreTarget(nameof(DtoUpdateGame.IsRemoved))]
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
