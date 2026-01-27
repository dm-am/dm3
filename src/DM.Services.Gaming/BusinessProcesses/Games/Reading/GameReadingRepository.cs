using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using DM.Services.Gaming.BusinessProcesses.Shared;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Gaming.BusinessProcesses.Games.Reading;

/// <inheritdoc />
internal class GameReadingRepository : IGameReadingRepository
{
    private readonly DmDbContext dbContext;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public GameReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public Task<int> Count(GamesQuery query, Guid userId)
    {
        return dbContext.Games
            .Where(AccessibilityFilters.GameAvailable(userId))
            .Where(g => query.Statuses.Contains(g.Status))
            .Where(g => !query.TagId.HasValue || g.GameTags.Any(t => t.TagId == query.TagId.Value))
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Game>> GetGames(PagingData pagingData, GamesQuery query, Guid userId)
    {
        return await dbContext.Games
            .Where(AccessibilityFilters.GameAvailable(userId))
            .Where(g => query.Statuses.Contains(g.Status))
            .Where(g => !query.TagId.HasValue || g.GameTags.Any(t => t.TagId == query.TagId.Value))
            .OrderBy(g => g.ReleaseDate ?? g.CreatedUtc)
            .Skip(pagingData.Skip)
            .Take(pagingData.Take)
            .ProjectTo<Game>(mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Game>> GetOwn(Guid userId)
    {
        // Optimized query with manual projection to avoid N+1 from ProjectTo
        // PendingAssistant mapping causes client-side evaluation
        return await dbContext.Games
            .Where(AccessibilityFilters.GameAvailable(userId))
            .Where(g => g.Characters.Any(c =>
                            !c.IsRemoved && c.Status == CharacterStatus.Active && c.UserId == userId) ||
                        g.Readers.Any(r => r.UserId == userId) ||
                        g.MasterId == userId || g.AssistantId == userId || g.MentorId == userId)
            .Select(g => new Game
            {
                Id = g.GameId,
                Title = g.Title,
                Status = g.Status,
                CreatedUtc = g.CreatedUtc,
                ReleaseDate = g.ReleaseDate,
                SystemName = g.SystemName,
                NarrativeSetting = g.NarrativeSetting,
                CommentariesAccessMode = g.CommentariesAccessMode,
                AttributeSchemaId = g.AttributeSchemaId,
                // Only load Master login (needed for tooltip and participation)
                Master = new Core.Dto.GeneralUser
                {
                    UserId = g.MasterId,
                    Login = g.Master.Login
                },
                // Only load Assistant UserId (needed for participation check)
                Assistant = g.AssistantId.HasValue ? new Core.Dto.GeneralUser
                {
                    UserId = g.AssistantId.Value
                } : null,
                // Only load Mentor UserId (needed for participation check)
                Mentor = g.MentorId.HasValue ? new Core.Dto.GeneralUser
                {
                    UserId = g.MentorId.Value
                } : null,
                // Skip PendingAssistant - not needed for sidebar
                PendingAssistant = null,
                // Active character user IDs (needed for player count and participation)
                ActiveCharacterUserIds = g.Characters
                    .Where(c => !c.IsRemoved && c.Status == CharacterStatus.Active)
                    .Select(c => c.UserId)
                    .ToList(),
                // Reader user IDs (needed for participation)
                ReaderUserIds = g.Readers.Select(r => r.UserId).ToList(),
                // Skip Tags - not needed for sidebar
                Tags = new List<GameTag>(),
                // Skip BlacklistedUsers - not needed for sidebar
                BlacklistedUsers = new List<BlacklistedUser>(),
                // Recruitment info for sidebar
                Recruitment = new GameRecruitment
                {
                    IsOpen = g.IsRecruitmentOpen,
                    PlayerLimit = g.RecruitmentPlayerLimit,
                    PlayerCount = g.Characters
                        .Where(c => !c.IsRemoved && c.Status == CharacterStatus.Active)
                        .Select(c => c.UserId)
                        .Distinct()
                        .Count(),
                    StartedUtc = g.RecruitmentStartedUtc
                }
            })
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, IEnumerable<Guid>>> GetAvailableRoomIds(IEnumerable<Guid> gameIds, Guid userId)
    {
        var roomsInGames = await dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .Where(r => gameIds.Contains(r.GameId))
            .Select(r => new {r.RoomId, r.GameId})
            .ToArrayAsync();
        return roomsInGames
            .GroupBy(g => g.GameId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RoomId));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PendingPost>> GetPendingPosts(IEnumerable<Guid> gameIds, Guid userId)
    {
        return await dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .Where(r => gameIds.Contains(r.GameId))
            .SelectMany(r => r.PendingPosts)
            .ProjectTo<PendingPost>(mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public Task<GameExtended> GetGameDetails(Guid gameId, Guid userId)
    {
        return dbContext.Games
            .Where(AccessibilityFilters.GameAvailable(userId))
            .Where(g => g.GameId == gameId)
            .ProjectTo<GameExtended>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public Task<Game> GetGame(Guid gameId, Guid userId)
    {
        return dbContext.Games
            .Where(AccessibilityFilters.GameAvailable(userId))
            .Where(g => g.GameId == gameId)
            .ProjectTo<Game>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GameTag>> GetTags()
    {
        return await dbContext.Tags
            .ProjectTo<GameTag>(mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Game>> GetPopularGames(int gamesCount)
    {
        return await dbContext.Games
            .Where(g => !g.IsRemoved)
            .Where(g => g.Status == GameStatus.Active)
            .OrderByDescending(g => g.Readers.Count)
            .Take(gamesCount)
            .ProjectTo<Game>(mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<(IDictionary<Guid, IEnumerable<Guid>> rooms, IEnumerable<PendingPost> pendingPosts)> GetRoomsAndPendingPosts(
        IEnumerable<Guid> gameIds, Guid userId)
    {
        var gameIdArray = gameIds.ToArray();

        var rooms = await dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .Where(r => gameIdArray.Contains(r.GameId))
            .Select(r => new { r.RoomId, r.GameId })
            .ToArrayAsync();

        var pendingPosts = await dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .Where(r => gameIdArray.Contains(r.GameId))
            .SelectMany(r => r.PendingPosts)
            .ProjectTo<PendingPost>(mapper.ConfigurationProvider)
            .ToArrayAsync();

        var roomsDict = rooms
            .GroupBy(g => g.GameId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RoomId));

        return (roomsDict, pendingPosts);
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, int>> GetTotalPostCounts(IEnumerable<Guid> gameIds)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await dbContext.Rooms
            .Where(r => !r.IsRemoved && gameIdList.Contains(r.GameId))
            .Select(r => new { r.GameId, PostCount = r.Posts.Count(p => !p.IsRemoved) })
            .ToArrayAsync();

        return counts
            .GroupBy(x => x.GameId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.PostCount));
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, int>> GetTotalCommentCounts(IEnumerable<Guid> gameIds)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return new Dictionary<Guid, int>();

        // Comments are linked to games via EntityId (same field used for forum topics)
        // Filter by checking if there's a matching game
        var counts = await dbContext.Comments
            .Where(c => !c.IsRemoved && gameIdList.Contains(c.EntityId))
            .GroupBy(c => c.EntityId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToArrayAsync();

        return counts.ToDictionary(x => x.GameId, x => x.Count);
    }
}