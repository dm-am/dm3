using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbTag = DM.Infrastructure.Persistence.Entities.Game.Links.GameTag;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Repository for game operations
/// </summary>
internal class GameRepository : IGameRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;

    public GameRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _guidFactory = guidFactory;
    }

    #region Read Operations

    public Task<int> Count(GamesQuery query, Guid userId, CancellationToken ct = default)
    {
        return _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => query.Statuses.Contains(g.Status))
            .Where(g => !query.TagId.HasValue || g.GameTags.Any(t => t.TagId == query.TagId.Value))
            .Where(g => !query.IsRecruiting.HasValue || g.IsRecruitmentOpen == query.IsRecruiting.Value)
            .Where(g => !query.IsFinished.HasValue || g.IsFinished == query.IsFinished.Value)
            .Where(g => string.IsNullOrEmpty(query.MasterUsername) || g.Author.Username == query.MasterUsername)
            .Where(g => string.IsNullOrEmpty(query.PlayerUsername) ||
                g.Characters.Any(c => !c.IsRemoved && c.Status == CharacterStatus.Active && c.Author!.Username == query.PlayerUsername))
            .Where(g => query.ExcludeMasterIds == null || !query.ExcludeMasterIds.Contains(g.AuthorId))
            .CountAsync(ct);
    }

    public async Task<IEnumerable<GameModel>> GetGames(PagingData pagingData, GamesQuery query, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => query.Statuses.Contains(g.Status))
            .Where(g => !query.TagId.HasValue || g.GameTags.Any(t => t.TagId == query.TagId.Value))
            .Where(g => !query.IsRecruiting.HasValue || g.IsRecruitmentOpen == query.IsRecruiting.Value)
            .Where(g => !query.IsFinished.HasValue || g.IsFinished == query.IsFinished.Value)
            .Where(g => string.IsNullOrEmpty(query.MasterUsername) || g.Author.Username == query.MasterUsername)
            .Where(g => string.IsNullOrEmpty(query.PlayerUsername) ||
                g.Characters.Any(c => !c.IsRemoved && c.Status == CharacterStatus.Active && c.Author!.Username == query.PlayerUsername))
            .Where(g => query.ExcludeMasterIds == null || !query.ExcludeMasterIds.Contains(g.AuthorId))
            .OrderByDescending(g => g.ReleaseDate ?? g.CreatedUtc)
            .Page(pagingData)
            .ProjectTo<GameModel>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);
    }

    public async Task<IEnumerable<GameModel>> GetOwn(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.Characters.Any(c =>
                            !c.IsRemoved && c.Status == CharacterStatus.Active && c.AuthorId == userId) ||
                        _dbContext.Subscriptions.Any(s =>
                            s.TargetType == SubscriptionTargetType.Game &&
                            s.TargetId == g.GameId &&
                            s.SubscriberId == userId) ||
                        g.AuthorId == userId || g.Assistants.Any(a => a.UserId == userId) || g.MentorId == userId)
            .OrderByDescending(g => g.ReleaseDate ?? g.CreatedUtc)
            .ProjectTo<GameModel>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);
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

    public async Task<IEnumerable<PostPendency>> GetPostPendencies(IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default)
    {
        var gameIdArray = gameIds.ToArray();

        var roomIds = await _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => gameIdArray.Contains(r.GameId))
            .Select(r => r.RoomId)
            .ToArrayAsync(ct);

        return await _dbContext.PostPendencies
            .Where(p => !p.IsRemoved && roomIds.Contains(p.RoomId))
            .ProjectTo<PostPendency>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);
    }

    public async Task<(IDictionary<Guid, IEnumerable<Guid>> rooms, IEnumerable<PostPendency> postPendencies)> GetRoomsAndPostPendencies(
        IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default)
    {
        var gameIdArray = gameIds.ToArray();

        var rooms = await _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => gameIdArray.Contains(r.GameId))
            .Select(r => new { r.RoomId, r.GameId })
            .ToArrayAsync(ct);

        var roomIds = rooms.Select(r => r.RoomId).ToArray();
        var postPendencies = await _dbContext.PostPendencies
            .Where(p => !p.IsRemoved && roomIds.Contains(p.RoomId))
            .ProjectTo<PostPendency>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        var roomsDict = rooms
            .GroupBy(g => g.GameId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RoomId));

        return (roomsDict, postPendencies);
    }

    public async Task<IDictionary<Guid, int>> GetTotalPostCounts(IEnumerable<Guid> gameIds, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await _dbContext.Rooms
            .Where(r => !r.IsRemoved && gameIdList.Contains(r.GameId))
            .Select(r => new { r.GameId, PostCount = r.Posts.Count(p => !p.IsRemoved) })
            .ToArrayAsync(ct);

        return counts
            .GroupBy(x => x.GameId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.PostCount));
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

    public Task<GameExtended?> GetGameDetails(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        return _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.GameId == gameId)
            .ProjectTo<GameExtended>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct)!;
    }

    public Task<GameModel?> GetGame(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        return _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.GameId == gameId)
            .ProjectTo<GameModel>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct)!;
    }

    public async Task<IEnumerable<GameTag>> GetTags(CancellationToken ct = default)
    {
        return await _dbContext.Tags
            .Select(t => new GameTag
            {
                Id = t.TagId,
                Title = t.Title,
                GroupTitle = t.TagGroup.Title,
                GamesCount = t.GameTags.Count(gt => gt.Game.Status == ModuleStatus.Active)
            })
            .ToArrayAsync(ct);
    }

    public async Task<IEnumerable<GameModel>> GetPopularGames(int gamesCount, CancellationToken ct = default)
    {
        return await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(Guid.Empty))
            .Where(g => g.Status == ModuleStatus.Active)
            .OrderByDescending(g => _dbContext.Subscriptions
                .Count(s => s.TargetType == SubscriptionTargetType.Game && s.TargetId == g.GameId))
            .Take(gamesCount)
            .ProjectTo<GameModel>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);
    }

    public async Task<IEnumerable<GameModel>> GetByIds(IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return Array.Empty<GameModel>();

        return await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => gameIdList.Contains(g.GameId))
            .ProjectTo<GameModel>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);
    }

    #endregion

    #region Write Operations

    public async Task<GameExtended> Create(CreateGameEntity game, CreateRoomEntity room, CancellationToken ct = default)
    {
        var dbGame = new DbGame
        {
            GameId = game.GameId,
            CreatedUtc = game.CreatedUtc,
            Status = game.Status,
            ReleaseDate = game.ReleaseDate,
            AuthorId = game.AuthorId,
            Title = game.Title.Trim(),
            SystemName = game.SystemName?.Trim(),
            NarrativeSetting = game.NarrativeSetting?.Trim(),
            Info = game.Info?.Trim(),
            HideTemper = game.HideTemper,
            HideSkills = game.HideSkills,
            HideInventory = game.HideInventory,
            HideStory = game.HideStory,
            DisableAlignment = game.DisableAlignment,
            HideDiceResult = game.HideDiceResult,
            ShowPrivateMessages = game.ShowPrivateMessages,
            HidePostStats = game.HidePostStats,
            CommentsAccessMode = game.CommentsAccessMode,
            AttributeSchemaId = game.AttributeSchemaId,
            IsRemoved = false,
            IsRecruitmentOpen = game.IsRecruitmentOpen,
            RecruitmentStartedUtc = game.RecruitmentStartedUtc
        };

        var dbRoom = new DbRoom
        {
            RoomId = room.RoomId,
            GameId = room.GameId,
            Title = room.Title.Trim(),
            Type = room.Type,
            AccessType = room.AccessType,
            ViewPrivateText = room.ViewPrivateText,
            ViewDiceResults = room.ViewDiceResults,
            DiceEnabled = room.DiceEnabled,
            OrderNumber = room.OrderNumber,
            IsRemoved = false
        };

        var dbTags = game.TagIds.Select(tagId => new DbTag
        {
            GameTagId = _guidFactory.Create(),
            GameId = game.GameId,
            TagId = tagId
        });

        await _dbContext.Games.AddAsync(dbGame, ct);
        await _dbContext.Rooms.AddAsync(dbRoom, ct);
        await _dbContext.GameTags.AddRangeAsync(dbTags, ct);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Games
            .Where(g => g.GameId == game.GameId)
            .ProjectTo<GameExtended>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    public async Task<GameExtended> Update(UpdateGameEntity updateGame, CancellationToken ct = default)
    {
        var game = await _dbContext.Games.FindAsync([updateGame.GameId], ct);
        if (game == null)
            throw new InvalidOperationException($"Game {updateGame.GameId} not found");

        // Update fields if provided
        if (updateGame.Status.HasValue)
            game.Status = updateGame.Status.Value;

        if (updateGame.PremoderationStatus.HasValue)
            game.PremoderationStatus = updateGame.PremoderationStatus.Value;

        if (updateGame.IsFinished.HasValue)
            game.IsFinished = updateGame.IsFinished.Value;

        if (updateGame.IsFrozen.HasValue)
            game.IsFrozen = updateGame.IsFrozen.Value;

        if (updateGame.IsRecruitmentOpen.HasValue)
            game.IsRecruitmentOpen = updateGame.IsRecruitmentOpen.Value;

        if (updateGame.RecruitmentPlayerLimit.HasValue)
            game.RecruitmentPlayerLimit = updateGame.RecruitmentPlayerLimit;

        if (!string.IsNullOrEmpty(updateGame.Title))
            game.Title = updateGame.Title.Trim();

        if (!string.IsNullOrEmpty(updateGame.SystemName))
            game.SystemName = updateGame.SystemName.Trim();

        if (!string.IsNullOrEmpty(updateGame.NarrativeSetting))
            game.NarrativeSetting = updateGame.NarrativeSetting.Trim();

        if (!string.IsNullOrEmpty(updateGame.Info))
            game.Info = updateGame.Info.Trim();

        if (updateGame.HideTemper.HasValue)
            game.HideTemper = updateGame.HideTemper.Value;

        if (updateGame.HideSkills.HasValue)
            game.HideSkills = updateGame.HideSkills.Value;

        if (updateGame.HideInventory.HasValue)
            game.HideInventory = updateGame.HideInventory.Value;

        if (updateGame.HideStory.HasValue)
            game.HideStory = updateGame.HideStory.Value;

        if (updateGame.DisableAlignment.HasValue)
            game.DisableAlignment = updateGame.DisableAlignment.Value;

        if (updateGame.HideDiceResult.HasValue)
            game.HideDiceResult = updateGame.HideDiceResult.Value;

        if (updateGame.ShowPrivateMessages.HasValue)
            game.ShowPrivateMessages = updateGame.ShowPrivateMessages.Value;

        if (updateGame.HidePostStats.HasValue)
            game.HidePostStats = updateGame.HidePostStats.Value;

        if (updateGame.CommentsAccessMode.HasValue)
            game.CommentsAccessMode = updateGame.CommentsAccessMode.Value;

        // Update tags if provided
        if (updateGame.TagIds != null && updateGame.TagIds.Any())
        {
            var existingTags = await _dbContext.GameTags
                .Where(t => t.GameId == updateGame.GameId)
                .ToListAsync(ct);

            _dbContext.GameTags.RemoveRange(existingTags);

            var newTags = updateGame.TagIds.Select(tagId => new DbTag
            {
                GameTagId = _guidFactory.Create(),
                GameId = updateGame.GameId,
                TagId = tagId
            });

            _dbContext.GameTags.AddRange(newTags);
        }

        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Games
            .Where(g => g.GameId == updateGame.GameId)
            .ProjectTo<GameExtended>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    public async Task Delete(Guid gameId, CancellationToken ct = default)
    {
        var game = await _dbContext.Games.FindAsync([gameId], ct);
        if (game != null)
        {
            game.IsRemoved = true;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    #endregion
}
