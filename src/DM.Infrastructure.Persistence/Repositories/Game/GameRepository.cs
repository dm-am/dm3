using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Attachments;
using GameDto = DM.Domain.Game.Features.Games.Game;
using Microsoft.EntityFrameworkCore;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbTag = DM.Infrastructure.Persistence.Entities.Game.Links.GameTag;

namespace DM.Infrastructure.Persistence.Repositories.Game;

internal partial class GameRepository : IGameRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly PublicIdService _publicIdService;

    public GameRepository(
        DmDbContext dbContext,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        PublicIdService publicIdService)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _publicIdService = publicIdService;
    }

    /// <summary>
    /// Populate the info-table aggregates that only the game details page needs
    /// (total / master post counts, last master post, dice support). Kept off
    /// the list and by-ids paths so those pay no extra queries. Every query is a
    /// plain aggregate / EXISTS over a single game - no collection Include, so it
    /// stays split-query safe and cannot cartesian-explode.
    /// </summary>
    private async Task EnrichGameDetailsAsync(GameDetails game, CancellationToken ct)
    {
        // A removed master is nested as nobody (RemovedAuthors), and his posts
        // still count as the master's: the identifier comes off the row then.
        var masterId = game.Master?.UserId ?? await _dbContext.Games
            .Where(g => g.GameId == game.Id)
            .Select(g => g.MasterId)
            .FirstAsync(ct);

        // Posts across all live rooms of the game.
        var gamePosts = _dbContext.Posts
            .Where(p => !p.IsRemoved && !p.Room.IsRemoved && p.Room.GameId == game.Id);

        game.TotalPostsCount = await gamePosts.CountAsync(ct);
        game.MasterPostsCount = await gamePosts.CountAsync(p => p.AuthorId == masterId, ct);
        game.LastMasterPostUtc = await gamePosts
            .Where(p => p.AuthorId == masterId)
            .MaxAsync(p => (DateTimeOffset?)p.CreatedUtc, ct);

        // Dice support = any live room of the game has dice rolling enabled.
        game.DiceSupported = await _dbContext.Rooms
            .AnyAsync(r => !r.IsRemoved && r.GameId == game.Id && r.DiceEnabled, ct);
    }

    public async Task<IDictionary<Guid, IEnumerable<Guid>>> GetAvailableRoomIds(IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default)
    {
        var gameIdArray = gameIds.ToArray();

        var rooms = await _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => gameIdArray.Contains(r.GameId))
            .Select(r => new { r.RoomId, r.GameId })
            .ToArrayAsync(ct);

        return rooms
            .GroupBy(g => g.GameId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RoomId));
    }

    public async Task<IDictionary<Guid, int>> GetTotalPostCounts(IEnumerable<Guid> gameIds, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return new Dictionary<Guid, int>();

        // Single GROUP BY query instead of N subqueries per room
        return await _dbContext.Posts
            .Where(p => !p.IsRemoved && !p.Room.IsRemoved && gameIdList.Contains(p.Room.GameId))
            .GroupBy(p => p.Room.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);
    }

    public async Task<IDictionary<Guid, int>> GetTotalCommentCounts(IEnumerable<Guid> gameIds, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await _dbContext.Comments
            .Where(c => !c.IsRemoved && gameIdList.Contains(c.EntityId))
            .GroupBy(c => c.EntityId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToArrayAsync(ct);

        return counts.ToDictionary(x => x.GameId, x => x.Count);
    }

    public Task<GameDetails?> GetGameDetails(
        Guid gameId, Guid userId, bool mayJudgePremoderation, CancellationToken ct = default) =>
        LoadGameDetails(g => g.GameId == gameId, userId, moderationScope: false, mayJudgePremoderation, ct);

    /// <inheritdoc />
    public Task<GameDetails?> GetGameDetailsForModeration(
        Guid gameId, Guid viewerId, CancellationToken ct = default) =>
        LoadGameDetails(g => g.GameId == gameId, viewerId,
            moderationScope: true, mayJudgePremoderation: true, ct);

    /// <inheritdoc />
    public Task<GameDetails?> GetGameDetailsByPublicIdForModeration(
        string publicId, Guid viewerId, CancellationToken ct = default) =>
        LoadGameDetails(g => g.PublicId == publicId, viewerId,
            moderationScope: true, mayJudgePremoderation: true, ct);

    /// <summary>
    /// The one body behind every single-game details read: by identifier, by
    /// alias, and the premoderation pair that skips the accessibility scope.
    /// </summary>
    /// <param name="addressing">Which game — by identifier or by public alias.</param>
    /// <param name="viewerId">Reader the projection and the enrichment answer for.</param>
    /// <param name="moderationScope">
    /// Drop the accessibility filter entirely. Only the premoderation endpoint
    /// passes true, and only behind its Mentor+ gate: the two verdict moves are
    /// legal from every status, so the game they are passed on may be an approved
    /// private draft, which the rank arm of the scope deliberately does not open.
    /// </param>
    /// <param name="mayJudgePremoderation">
    /// Whether the viewer holds the rank that passes premoderation verdicts. Adds
    /// the games awaiting one to the ordinary scope, and nothing else. Ignored
    /// when <paramref name="moderationScope" /> already dropped the filter.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    private async Task<GameDetails?> LoadGameDetails(
        Expression<Func<DbGame, bool>> addressing,
        Guid viewerId,
        bool moderationScope,
        bool mayJudgePremoderation,
        CancellationToken ct)
    {
        var games = _dbContext.Games.AsQueryable();
        games = moderationScope
            ? games.Where(g => !g.IsRemoved)
            : games.Where(GameAccessibilityFilters.GameAvailable(viewerId, mayJudgePremoderation));

        var game = await games
            .Where(addressing)
            .ProjectToGameDetails()
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (game is not null)
        {
            await EnrichGamesAsync(new[] { game }, viewerId, ct);
            await EnrichGameDetailsAsync(game, ct);
        }
        return game;
    }

    public Task<GameDto?> GetGame(
        Guid gameId, Guid userId, bool mayJudgePremoderation, CancellationToken ct = default) =>
        ReadOne(g => g.GameId == gameId, userId, mayJudgePremoderation, ct);

    public Task<Guid?> FindGameIdByPublicId(
        string publicId, Guid userId, bool mayJudgePremoderation, CancellationToken ct = default)
    {
        // Same visibility filter as the aggregate read, so the set of public ids
        // that resolve is identical — but one scalar column instead of a game,
        // its players, its readers and its unread counters, all of which the
        // callers of this throw away.
        return _dbContext.Games
            .TagWith("DM.Game.FindIdByPublicId")
            .Where(GameAccessibilityFilters.GameAvailable(userId, mayJudgePremoderation))
            .Where(g => g.PublicId == publicId)
            .Select(g => (Guid?)g.GameId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<GameDto?> GetGameByPublicId(
        string publicId, Guid userId, bool mayJudgePremoderation, CancellationToken ct = default) =>
        ReadOne(g => g.PublicId == publicId, userId, mayJudgePremoderation, ct);

    /// <summary>
    /// One game as the user reads it. Enriched here because every authorization
    /// decision about a game reads its players, readers and pending invitations.
    /// </summary>
    private async Task<GameDto?> ReadOne(
        Expression<Func<DbGame, bool>> predicate, Guid userId, bool mayJudgePremoderation, CancellationToken ct)
    {
        var game = await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId, mayJudgePremoderation))
            .Where(predicate)
            .ProjectToGame()
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (game is not null)
        {
            await EnrichGamesAsync(new[] { game }, userId, ct);
        }

        return game;
    }

    public Task<GameDetails?> GetGameDetailsByPublicId(
        string publicId, Guid userId, bool mayJudgePremoderation, CancellationToken ct = default) =>
        LoadGameDetails(g => g.PublicId == publicId, userId, moderationScope: false, mayJudgePremoderation, ct);

    public async Task<IEnumerable<GameDto>> GetByIds(
        IEnumerable<Guid> gameIds, Guid userId, Guid viewerId, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return Array.Empty<GameDto>();

        var games = await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => gameIdList.Contains(g.GameId))
            .ProjectToGame()
            .AsSplitQuery()
            .ToArrayAsync(ct);

        await EnrichGamesAsync(games, viewerId, ct);
        return games;
    }

    public async Task<GameDetails> Create(CreateGameEntity game, CreateRoomEntity room, CancellationToken ct = default)
    {
        var dbGame = NewGameRow(game);
        var dbRoom = NewFirstRoom(room);
        var dbTags = NewTags(game);
        var dbSchema = NewSheet(game);

        // The readable address is taken before the insert instead of being stamped by
        // a second SaveChanges — see SerialNumberAllocator for what that pair cost.
        dbGame.SerialNumber = await SerialNumberAllocator.NextAsync<DbGame>(_dbContext, ct);
        dbGame.PublicId = _publicIdService.Encode(dbGame.SerialNumber);

        await _dbContext.Games.AddAsync(dbGame, ct);
        await _dbContext.Rooms.AddAsync(dbRoom, ct);
        await _dbContext.GameTags.AddRangeAsync(dbTags, ct);
        await _dbContext.GameAttributeSchemas.AddAsync(dbSchema, ct);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Games
            .Where(g => g.GameId == game.GameId)
            .ProjectToGameDetails()
            .AsSplitQuery()
            .FirstAsync(ct);
    }

    public async Task<GameDetails> Update(UpdateGameEntity updateGame, CancellationToken ct = default)
    {
        var game = await _dbContext.Games.FindAsync([updateGame.GameId], ct);
        if (game == null)
            throw new InvalidOperationException($"Game {updateGame.GameId} not found");

        // Update fields if provided
        ApplyStatus(game, updateGame);
        ApplySettings(game, updateGame);

        // Update tags if provided. Null and empty are not the same answer: null
        // is the update saying nothing about tags, an empty list is the master
        // taking them all off, and the `.Any()` this once also carried made the
        // second one unsayable.
        await ReplaceTags(updateGame, ct);

        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Games
            .Where(g => g.GameId == updateGame.GameId)
            .ProjectToGameDetails()
            .AsSplitQuery()
            .FirstAsync(ct);
    }

    private static DbGame NewGameRow(CreateGameEntity game) => new()
    {
        GameId = game.GameId,
        CreatedUtc = game.CreatedUtc,
        Status = game.Status,
        PremoderationStatus = game.PremoderationStatus,
        DraftVisibility = game.DraftVisibility,
        ActivatedUtc = game.ActivatedUtc,
        MasterId = game.MasterId,
        Title = game.Title,
        SystemName = game.SystemName,
        NarrativeSetting = game.NarrativeSetting,
        Info = game.Info,
        HideDiceResult = game.HideDiceResult,
        ShowPrivateMessages = game.ShowPrivateMessages,
        HidePostStats = game.HidePostStats,
        CommentsAccessMode = game.CommentsAccessMode,
        IsRemoved = false,
        IsRecruitmentOpen = game.IsRecruitmentOpen,
        RecruitmentStartedUtc = game.RecruitmentStartedUtc,
        RecruitmentCount = game.RecruitmentCount
    };

    private static DbRoom NewFirstRoom(CreateRoomEntity room) => new()
    {
        RoomId = room.RoomId,
        GameId = room.GameId,
        // The game itself is inserted by this call, so nobody else can hold
        // a room in it yet and its first room is number 1 and first in the
        // list by construction. Every later room takes MAX+1 of both under
        // the game row lock, in RoomRepository.Create.
        RoomNumber = 1,
        OrderNumber = 1,
        Title = room.Title,
        Type = room.Type,
        AccessType = room.AccessType,
        ViewPrivateText = room.ViewPrivateText,
        ViewDiceResults = room.ViewDiceResults,
        DiceEnabled = room.DiceEnabled,
        IsRemoved = false
    };

    // A set, not a list: the same tag twice is the same tag, and the pair is
    // unique in the schema.
    private IEnumerable<DbTag> NewTags(CreateGameEntity game) => game.TagIds.Distinct().Select(tagId => new DbTag
    {
        GameTagId = _guidFactory.Create(),
        GameId = game.GameId,
        TagId = tagId
    });

    // The character sheet goes in with the game, in the same save: a
    // creation that failed after the game row would otherwise leave a game
    // whose characters cannot be described, and every read of a game asks
    // for its sheet. The identifiers are the server's, all of them - a
    // caller has nothing yet to keep.
    private Entities.Game.GameAttributeSchema NewSheet(CreateGameEntity game) => new()
    {
        GameId = game.GameId,
        Specifications = AttributeSpecificationDocument.ToDocument(
            game.AttributeSpecifications, _ => _guidFactory.Create()),
        Version = 0,
        UpdatedUtc = game.CreatedUtc
    };

    private static void ApplyStatus(DbGame game, UpdateGameEntity updateGame)
    {
        if (updateGame.Status.HasValue)
        {
            // Reset inactivity warnings when game is reactivated
            if (updateGame.Status.Value == ModuleStatus.Active && game.Status != ModuleStatus.Active)
            {
                game.InactivityWarningUtc = null;
                game.ClosureWarningUtc = null;
            }
            game.Status = updateGame.Status.Value;
        }

        if (updateGame.PremoderationStatus.HasValue)
            game.PremoderationStatus = updateGame.PremoderationStatus.Value;

        if (updateGame.ClosedReason.HasValue)
            game.ClosedReason = updateGame.ClosedReason.Value;

        if (updateGame.ActivatedUtc.HasValue)
            game.ActivatedUtc = updateGame.ActivatedUtc.Value;

        if (updateGame.ClosedUtc.HasValue)
            game.ClosedUtc = updateGame.ClosedUtc.Value;

        if (updateGame.ClearClosedUtc)
            game.ClosedUtc = null;

        if (updateGame.SetMentorId)
            game.MentorId = updateGame.MentorId;
    }

    private static void ApplySettings(DbGame game, UpdateGameEntity updateGame)
    {
        if (updateGame.IsRecruitmentOpen.HasValue)
            game.IsRecruitmentOpen = updateGame.IsRecruitmentOpen.Value;

        if (updateGame.IncrementRecruitmentCount)
            game.RecruitmentCount++;

        if (updateGame.RecruitmentPcLimit.HasValue)
            game.RecruitmentPcLimit = updateGame.RecruitmentPcLimit;

        if (!string.IsNullOrEmpty(updateGame.Title))
            game.Title = updateGame.Title;

        if (!string.IsNullOrEmpty(updateGame.SystemName))
            game.SystemName = updateGame.SystemName;

        if (!string.IsNullOrEmpty(updateGame.NarrativeSetting))
            game.NarrativeSetting = updateGame.NarrativeSetting;

        if (!string.IsNullOrEmpty(updateGame.Info))
            game.Info = updateGame.Info;

        if (updateGame.HideDiceResult.HasValue)
            game.HideDiceResult = updateGame.HideDiceResult.Value;

        if (updateGame.ShowPrivateMessages.HasValue)
            game.ShowPrivateMessages = updateGame.ShowPrivateMessages.Value;

        if (updateGame.HidePostStats.HasValue)
            game.HidePostStats = updateGame.HidePostStats.Value;

        if (updateGame.CommentsAccessMode.HasValue)
            game.CommentsAccessMode = updateGame.CommentsAccessMode.Value;

        if (updateGame.ClearRecruitmentStartedUtc)
            game.RecruitmentStartedUtc = null;
    }

    private async Task ReplaceTags(UpdateGameEntity updateGame, CancellationToken ct)
    {
        if (updateGame.TagIds != null)
        {
            // The difference, not a wholesale replacement: deleting a row and
            // inserting another one for the same pair in a single SaveChanges puts two
            // statements against one unique key into one batch, and nothing here needs
            // that. Rows that stay are left alone, which also keeps their identifiers.
            var requestedTagIds = updateGame.TagIds.Distinct().ToList();

            var existingTags = await _dbContext.GameTags
                .Where(t => t.GameId == updateGame.GameId)
                .ToListAsync(ct);

            _dbContext.GameTags.RemoveRange(
                existingTags.Where(t => !requestedTagIds.Contains(t.TagId)));

            var existingTagIds = existingTags.Select(t => t.TagId).ToHashSet();
            var newTags = requestedTagIds
                .Where(tagId => !existingTagIds.Contains(tagId))
                .Select(tagId => new DbTag
                {
                    GameTagId = _guidFactory.Create(),
                    GameId = updateGame.GameId,
                    TagId = tagId
                });

            _dbContext.GameTags.AddRange(newTags);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The attachments of the posts and the subscriptions to the game go in the same
    /// transaction: nothing would ever serve, collect or deliver them after it.
    /// </remarks>
    public async Task Delete(Guid gameId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var game = await _dbContext.Games.FindAsync([gameId], ct);
        if (game == null)
        {
            return;
        }

        var now = _dateTimeProvider.Now;

        // The strategy wrapper is required because the API host configures
        // EnableRetryOnFailure.
        await RetryableWrite.Run(_dbContext, async isRetry =>
        {
            if (isRetry)
            {
                // The clear the helper just did detached the game this block is
                // about to soft-delete, so it has to be read again - and it may be
                // gone by now.
                game = await _dbContext.Games.FindAsync([gameId], ct);
                if (game == null)
                {
                    return;
                }
            }
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            SoftDelete.Mark(game, deletedByUserId, now);
            await _dbContext.SaveChangesAsync(ct);

            // Live rows only, by the global soft-delete filter: a file already
            // taken off a post keeps the grace period it started with.
            await AttachmentRows.RetireAsync(
                _dbContext.Uploads
                    .Where(u => u.Type == UploadType.PostAttachment &&
                                u.TargetPostId != null &&
                                _dbContext.Posts.Any(p =>
                                    p.PostId == u.TargetPostId!.Value && p.Room.GameId == gameId)),
                deletedByUserId, now, ct);

            await _dbContext.Subscriptions
                .Where(s => s.TargetType == SubscriptionTargetType.Game && s.TargetId == gameId)
                .ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);
        });
    }
}
