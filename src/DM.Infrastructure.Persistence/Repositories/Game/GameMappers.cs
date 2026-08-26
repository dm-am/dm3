using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbTag = DM.Infrastructure.Persistence.Entities.Shared.Tag;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbPostPendency = DM.Infrastructure.Persistence.Entities.Game.Links.PostPendency;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DtoGameTag = DM.Domain.Game.Features.Games.GameTag;
using DtoRoomAccess = DM.Domain.Game.Features.Games.RoomAccess;
using DtoPostPendency = DM.Domain.Game.Features.Games.PostPendency;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Projection formulas for rooms, characters and games.
///
/// The shared shapes (a room access, a pendency, a character, a tag, the
/// room itself) are written once as expressions and inlined with
/// <see cref="ExpressionSplicer"/> wherever they nest; the pendency
/// SELECTION comes whole from <see cref="PostPendencyFilters.OfRoom"/> - the
/// rule behind every turn star on the site - so a game can never claim a
/// turn is awaited that the room behind it does not show.
///
/// The list DTO and the details are one member list, not two: the details
/// formula is built from the list formula's bindings with
/// <see cref="ExpressionSplicer.WithBaseBindings{TSource,TBase,TTier}"/> and
/// only appends its own members. The room and the room-for-update are joined
/// the same way.
///
/// This file used to claim that an initializer cannot be shared across a base
/// and a derived target, and kept two copies of each list "identical member
/// for member" by hand. The claim was false: GeneralUserProjections had been
/// merging GeneralUser with AuthenticatedUser and UserDetails by exactly this
/// mechanism all along. What the copies bought was drift - a member added to
/// one target and forgotten in the other, which no build and no test reports.
/// </summary>
public static class GameMappers
{
    // ═══ Shared member formulas (spliced, never invoked) ═══

    private static readonly Expression<Func<DbTag, DtoGameTag>> TagProjection =
        t => new DtoGameTag
        {
            Id = t.TagId,
            ShortId = t.ShortId,
            GroupTitle = t.TagGroup.Title,
            GroupDescription = t.TagGroup.Description,
            GroupSortOrder = t.TagGroup.SortOrder,
            GroupMaxTagsPerGame = t.TagGroup.MaxTagsPerGame,
            Title = t.Title,
            Description = t.Description,
            SortOrder = t.SortOrder
            // GamesCount is computed at runtime by the tag catalog read.
        };

    private static readonly Expression<Func<DbPostPendency, DtoPostPendency>> PendencyProjection =
        p => new DtoPostPendency
        {
            Id = p.PendencyId,
            RoomId = p.RoomId,
            CharacterId = p.CharacterId,
            CharacterName = p.Character.Name,
            CreatedBy = GeneralUserProjections.Projection.Splice(p.CreatedBy),
            WaitingForUser = GeneralUserProjections.Projection.Splice(p.WaitingForUser),
            CreatedUtc = p.CreatedUtc,
            FulfilledUtc = p.FulfilledUtc
        };

    // Picture is loaded by a separate batched query (PostRepository /
    // CharacterRepository); ModifiedUtc is not stored on the DB entity yet;
    // Descriptor is derived from the schema by CharacterAttributeValueFiller
    // in the domain layer. LastPostUtc is an aggregate subquery (MAX), not a
    // collection join - split-query safe.
    private static readonly Expression<Func<DbCharacter, Character>> CharacterProjection =
        c => new Character
        {
            Id = c.CharacterId,
            GameId = c.GameId,
            CreatedUtc = c.CreatedUtc,
            Status = c.Status,
            IsDead = c.IsDead,
            IsPlayerLeft = c.IsPlayerLeft,
            IsPlayerExiled = c.IsPlayerExiled,
            TotalPostsCount = c.Posts.Count(),
            LastPostUtc = c.Posts.Max(p => (DateTimeOffset?)p.CreatedUtc),
            Author = GeneralUserProjections.Projection.Splice(c.Author),
            Name = c.Name,
            IsNpc = c.IsNpc,
            AccessPolicy = c.AccessPolicy,
            // The value filler resolves titles, types and modifiers from the
            // schema; the row carries only the raw value.
            Attributes = c.Attributes
                .Select(a => new CharacterAttribute { Id = a.AttributeId, Value = a.Value })
                .ToList()
        };

    private static readonly Expression<Func<DbRoomAccess, DtoRoomAccess>> AccessProjection =
        l => new DtoRoomAccess
        {
            Id = l.AccessId,
            RoomId = l.RoomId,
            TargetType = l.CharacterId.HasValue
                ? RoomAccessTargetType.Character
                : RoomAccessTargetType.Reader,
            Policy = l.Policy,
            Character = l.Character == null ? null! : CharacterProjection.Splice(l.Character),
            // The reader of a character grant is the character's author; the
            // grant timestamp and grantor are not stored.
            User = l.CharacterId.HasValue
                ? GeneralUserProjections.Projection.Splice(l.Character!.Author)
                : GeneralUserProjections.Projection.Splice(l.ReaderUser)
        };

    // CanView and Description are set by the repository; the unread counter
    // is per reader. TotalPostsCount is a projection subquery over live
    // posts, mirroring the rubric counts on blogs.
    private static readonly Expression<Func<DbRoom, Room>> RoomProjection =
        r => new Room
        {
            Id = r.RoomId,
            RoomNumber = r.RoomNumber,
            GameId = r.GameId,
            ChatId = r.ChatId,
            Title = r.Title,
            Type = r.Type,
            AccessType = r.AccessType,
            IsArchived = r.IsArchived,
            OrderNumber = r.OrderNumber,
            PreviousRoomId = r.PreviousRoomId,
            TotalPostsCount = r.Posts.Count(p => !p.IsRemoved),
            Accesses = r.RoomAccesses.Select(a => AccessProjection.Splice(a)).ToList(),
            Pendencies = PostPendencyFilters.OfRoom.Splice(r)
                .Select(p => PendencyProjection.Splice(p))
                .ToList(),
            Settings = new RoomSettings
            {
                ViewPrivateText = r.ViewPrivateText,
                ViewDiceResults = r.ViewDiceResults,
                DiceEnabled = r.DiceEnabled,
                HiddenWithoutAccess = r.HiddenWithoutAccess
            }
        };

    private static readonly Expression<Func<DbGame, GameDto>> GameProjection =
        g => new GameDto
        {
            Id = g.GameId,
            PublicId = g.PublicId,
            CreatedUtc = g.CreatedUtc,
            Status = g.Status,
            PremoderationStatus = g.PremoderationStatus,
            ClosedReason = g.ClosedReason,
            DraftVisibility = g.DraftVisibility,
            ClosedUtc = g.ClosedUtc,
            ActivatedUtc = g.ActivatedUtc,
            AttributeSchemaId = g.AttributeSchemaId,
            Title = g.Title,
            SystemName = g.SystemName!,
            NarrativeSetting = g.NarrativeSetting!,
            CommentsAccessMode = g.CommentsAccessMode,
            CommentCount = g.CommentCount,
            LastCommentId = g.LastCommentId,
            Master = GeneralUserProjections.Projection.Splice(g.Master),
            Mentor = GeneralUserProjections.Projection.Splice(g.Mentor),
            Tags = g.GameTags.Select(t => TagProjection.Splice(t.Tag)).ToList(),
            TagIds = g.GameTags.Select(t => t.Tag.ShortId).ToList(),
            Assistants = g.Assistants
                .Select(a => new GameAssistantInfo
                {
                    UserId = a.UserId,
                    Username = a.User.Username,
                    JoinedUtc = a.JoinedUtc,
                    LastActivityUtc = a.User.LastActivityUtc,
                    Role = a.User.Role,
                    IsNewbie = a.User.IsNewbie
                })
                .ToList(),
            BlacklistedUsers = g.BlackList
                .Select(l => new BlacklistedUser { UserId = l.BlockedUserId, LinkId = l.EntryId })
                .ToList(),
            Recruitment = new GameRecruitment
            {
                IsOpen = g.IsRecruitmentOpen,
                PcLimit = g.RecruitmentPcLimit,
                // PcCount, like the subscriber facts and the viewer-scoped
                // members, is populated via batch queries in the repository.
                PcCount = 0,
                StartedUtc = g.RecruitmentStartedUtc,
                IsSubsequent = g.RecruitmentCount >= 2
            }
        };

    // The room plus its parent game, for authorization checks: the room
    // formula's members and nothing else changed about them.
    private static readonly Expression<Func<DbRoom, RoomToUpdate>> RoomToUpdateProjection =
        ExpressionSplicer.WithBaseBindings<DbRoom, Room, RoomToUpdate>(
            RoomProjection,
            r => new RoomToUpdate
            {
                Game = GameProjection.Splice(r.Game)
            });

    /// <summary>
    /// The list shape plus rooms, the roster and the public info. The
    /// read-side aggregates (post totals, dice support) have no entity
    /// counterpart and are computed by GameRepository after the projection.
    /// </summary>
    private static readonly Expression<Func<DbGame, GameDetails>> GameDetailsProjection =
        ExpressionSplicer.WithBaseBindings<DbGame, GameDto, GameDetails>(
            GameProjection,
            g => new GameDetails
            {
                Info = g.Info!,
                HideDiceResult = g.HideDiceResult,
                ShowPrivateMessages = g.ShowPrivateMessages,
                HidePostStats = g.HidePostStats,
                Rooms = g.Rooms.Select(r => RoomProjection.Splice(r)).ToList(),
                FullAssistants = g.Assistants
                    .Select(a => GeneralUserProjections.Projection.Splice(a.User))
                    .ToList(),
                Characters = g.Characters
                    .Select(c => new CharacterShortInfo
                    {
                        Id = c.CharacterId,
                        Author = GeneralUserProjections.Projection.Splice(c.Author),
                        Status = c.Status,
                        Name = c.Name,
                        IsNpc = c.IsNpc,
                        AccessPolicy = c.AccessPolicy,
                        PostsCount = c.Posts.Count()
                    })
                    .ToList()
            });

    // ═══ Queryable projections ═══

    /// <summary>EF projection to the domain room</summary>
    public static IQueryable<Room> ProjectToRoom(this IQueryable<DbRoom> query) =>
        query.Select(ExpressionSplicer.Expand(RoomProjection));

    /// <summary>
    /// EF projection to the room-for-update shape: the room plus its parent
    /// game, for authorization checks
    /// </summary>
    public static IQueryable<RoomToUpdate> ProjectToRoomToUpdate(this IQueryable<DbRoom> query) =>
        query.Select(ExpressionSplicer.Expand(RoomToUpdateProjection));

    /// <summary>EF projection to the room reorder shape</summary>
    public static IQueryable<RoomOrderInfo> ProjectToRoomOrderInfo(this IQueryable<DbRoom> query) =>
        query.Select(r => new RoomOrderInfo { Id = r.RoomId, OrderNumber = r.OrderNumber });

    /// <summary>EF projection to the domain room access</summary>
    public static IQueryable<DtoRoomAccess> ProjectToRoomAccess(this IQueryable<DbRoomAccess> query) =>
        query.Select(ExpressionSplicer.Expand(AccessProjection));

    /// <summary>EF projection to the domain pendency</summary>
    public static IQueryable<DtoPostPendency> ProjectToPostPendency(this IQueryable<DbPostPendency> query) =>
        query.Select(ExpressionSplicer.Expand(PendencyProjection));

    /// <summary>EF projection to the full domain character</summary>
    public static IQueryable<Character> ProjectToCharacter(this IQueryable<DbCharacter> query) =>
        query.Select(ExpressionSplicer.Expand(CharacterProjection));

    /// <summary>
    /// EF projection to the character-for-update shape, flattening the
    /// game-side facts the authorization rules read
    /// </summary>
    public static IQueryable<CharacterToUpdate> ProjectToCharacterToUpdate(this IQueryable<DbCharacter> query) =>
        query.Select(c => new CharacterToUpdate
        {
            Id = c.CharacterId,
            GameId = c.GameId,
            AuthorId = c.AuthorId ?? Guid.Empty,
            GameMasterId = c.Game.MasterId,
            GameAssistantIds = c.Game.Assistants.Select(a => a.UserId).ToList(),
            GameStatus = c.Game.Status,
            Status = c.Status,
            IsNpc = c.IsNpc,
            AccessPolicy = c.AccessPolicy,
            IsDead = c.IsDead,
            IsPlayerLeft = c.IsPlayerLeft,
            CreatedUtc = c.CreatedUtc
        });

    /// <summary>EF projection to the domain game (list shape)</summary>
    public static IQueryable<GameDto> ProjectToGame(this IQueryable<DbGame> query) =>
        query.Select(ExpressionSplicer.Expand(GameProjection));

    /// <summary>EF projection to the game details</summary>
    public static IQueryable<GameDetails> ProjectToGameDetails(this IQueryable<DbGame> query) =>
        query.Select(ExpressionSplicer.Expand(GameDetailsProjection));
}
