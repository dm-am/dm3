using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class RoomRepository : IRoomRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public RoomRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    #region Read Operations

    public async Task<IEnumerable<Room>> GetAllVisible(
        Guid gameId, Guid userId, bool mayJudgePremoderation)
    {
        var visible = _dbContext.Rooms
            .Where(r => r.GameId == gameId)
            .Where(GameAccessibilityFilters.RoomAvailable(
                userId, listingOnly: true, mayJudgePremoderation: mayJudgePremoderation));

        // Which of them the reader may actually open, asked with the very
        // filter every point read uses: a row in the menu and the page behind
        // it can then never disagree about who gets in. The rank goes into both
        // asks for that reason — handed to one of them only, the menu would name
        // rooms the point read then answered 404 for.
        var enterable = (await visible
            .Where(GameAccessibilityFilters.RoomAvailable(
                userId, mayJudgePremoderation: mayJudgePremoderation))
            .Select(r => r.RoomId)
            .ToArrayAsync())
            .ToHashSet();

        var rooms = await visible
            .InGameOrder()
            .ProjectToRoom()
            .ToArrayAsync();

        foreach (var room in rooms)
        {
            room.CanView = enterable.Contains(room.Id);
        }

        await CountChatMessages(rooms);
        return rooms;
    }

    public async Task<Room?> GetAvailable(Guid roomId, Guid userId, bool mayJudgePremoderation)
    {
        var room = await _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .Where(GameAccessibilityFilters.RoomAvailable(
                userId, mayJudgePremoderation: mayJudgePremoderation))
            .ProjectToRoom()
            .FirstOrDefaultAsync();

        if (room != null)
        {
            await CountChatMessages([room]);
        }

        return room;
    }

    public Task<RoomToUpdate?> GetForUpdate(Guid roomId, Guid userId)
    {
        return _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .ProjectToRoomToUpdate()
            .FirstOrDefaultAsync()!;
    }

    /// <summary>
    /// The messages of the chats behind the rooms of chat, one grouped read for
    /// all of them: the projection counts posts, and a room of chat has none.
    /// </summary>
    private async Task CountChatMessages(IReadOnlyCollection<Room> rooms)
    {
        var chatIds = rooms
            .Where(r => r.Type == RoomType.Chat && r.ChatId.HasValue)
            .Select(r => r.ChatId!.Value)
            .Distinct()
            .ToArray();
        if (chatIds.Length == 0)
        {
            return;
        }

        var counts = await _dbContext.Messages
            .Where(m => !m.IsRemoved && chatIds.Contains(m.ChatId))
            .GroupBy(m => m.ChatId)
            .Select(g => new { ChatId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(c => c.ChatId, c => c.Count);

        foreach (var room in rooms.Where(r => r.Type == RoomType.Chat && r.ChatId.HasValue))
        {
            room.TotalPostsCount = counts.GetValueOrDefault(room.ChatId!.Value);
        }
    }

    #endregion

    #region Write Operations

    public async Task<Room> Create(CreateRoomEntity createRoom)
    {
        // RoomNumber is the room's address and is allocated as MAX+1, which is
        // wrong the moment two rooms of one game are created at once. Locking
        // the game row first serializes creation per game, the smallest scope
        // that makes the number correct; the unique index on (GameId,
        // RoomNumber) stays as the invariant behind it. The strategy wrapper is
        // required because the API host configures EnableRetryOnFailure, and a
        // retrying execution strategy refuses a transaction opened by hand.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            await LockGame(createRoom.GameId);

            // IgnoreQueryFilters: a removed room keeps its number. Counted under
            // the soft-delete filter, deleting the newest room would hand its
            // number to the next one and the link of the deleted room would
            // start opening a different room.
            var maxRoomNumber = await _dbContext.Rooms
                .IgnoreQueryFilters()
                .TagWith("DM.Game.MaxRoomNumber")
                .Where(r => r.GameId == createRoom.GameId)
                .Select(r => (int?)r.RoomNumber)
                .MaxAsync() ?? 0;

            // The place after the last room the game lists, read under the
            // same lock: two rooms created at once would otherwise both take
            // it. A removed room is listed nowhere and holds no place.
            var lastPlace = await _dbContext.Rooms
                .TagWith("DM.Game.MaxRoomOrder")
                .Where(r => r.GameId == createRoom.GameId)
                .Select(r => (double?)r.OrderNumber)
                .MaxAsync() ?? 0;

            _dbContext.Rooms.Add(new DbRoom
            {
                RoomId = createRoom.RoomId,
                GameId = createRoom.GameId,
                RoomNumber = maxRoomNumber + 1,
                Title = createRoom.Title,
                Type = createRoom.Type,
                AccessType = createRoom.AccessType,
                ViewPrivateText = createRoom.ViewPrivateText,
                ViewDiceResults = createRoom.ViewDiceResults,
                DiceEnabled = createRoom.DiceEnabled,
                IsArchived = createRoom.IsArchived,
                OrderNumber = lastPlace + 1,
                IsRemoved = false
            });

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        });

        return await _dbContext.Rooms
            .Where(r => r.RoomId == createRoom.RoomId)
            .ProjectToRoom()
            .FirstAsync();
    }

    public async Task<Room> Update(UpdateRoomEntity updateRoom)
    {
        // A move rewrites the place of every room of the game, so it is
        // serialized per game the way creation is: two moves read side by side
        // would each number the rooms from the order the other is replacing.
        // The lock is taken before the room is read, so the values the change
        // tracker compares against are the ones the lock protects.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            if (updateRoom.ShouldReorder)
            {
                // A room never changes its game, so reading which one it is
                // ahead of the lock reads nothing the lock protects.
                var gameId = await _dbContext.Rooms
                    .Where(r => r.RoomId == updateRoom.RoomId)
                    .Select(r => r.GameId)
                    .FirstAsync();
                await LockGame(gameId);
            }

            var room = await _dbContext.Rooms.FindAsync(updateRoom.RoomId)
                ?? throw new InvalidOperationException($"Room {updateRoom.RoomId} not found");

            if (!string.IsNullOrEmpty(updateRoom.Title))
                room.Title = updateRoom.Title.Trim();

            if (updateRoom.Type.HasValue)
                room.Type = updateRoom.Type.Value;

            if (updateRoom.AccessType.HasValue)
                room.AccessType = updateRoom.AccessType.Value;

            if (updateRoom.ViewPrivateText.HasValue)
                room.ViewPrivateText = updateRoom.ViewPrivateText.Value;

            if (updateRoom.ViewDiceResults.HasValue)
                room.ViewDiceResults = updateRoom.ViewDiceResults.Value;

            if (updateRoom.DiceEnabled.HasValue)
                room.DiceEnabled = updateRoom.DiceEnabled.Value;

            if (updateRoom.HiddenWithoutAccess.HasValue)
                room.HiddenWithoutAccess = updateRoom.HiddenWithoutAccess.Value;

            if (updateRoom.IsArchived.HasValue)
                room.IsArchived = updateRoom.IsArchived.Value;

            if (updateRoom.ShouldSetChatId)
                room.ChatId = updateRoom.ChatId;

            if (updateRoom.ShouldReorder)
                await MoveAfter(room, updateRoom.NewPreviousRoomId);

            if (updateRoom.IsRemoved.HasValue)
                room.IsRemoved = updateRoom.IsRemoved.Value;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        });

        var result = await _dbContext.Rooms
            .Where(r => r.RoomId == updateRoom.RoomId)
            .ProjectToRoom()
            .FirstAsync();
        await CountChatMessages([result]);
        return result;
    }

    /// <summary>
    /// Puts the room right after <paramref name="previousRoomId" />, or first
    /// when it is null, and numbers the rooms of the game 1, 2, 3 in the order
    /// that makes. Every list sorts by that number (RoomOrder), so the move is
    /// seen by all of them at once.
    /// </summary>
    /// <remarks>
    /// The whole game is renumbered rather than one number squeezed between
    /// two: halving the gap runs out of precision after some fifty moves into
    /// one place, and a game holds a few dozen rooms at most. The service has
    /// checked the room named is one of this game; one removed since then
    /// leaves the room where it stands.
    /// </remarks>
    private async Task MoveAfter(DbRoom room, Guid? previousRoomId)
    {
        var order = await _dbContext.Rooms
            .Where(r => r.GameId == room.GameId)
            .InGameOrder()
            .ToListAsync();

        if (previousRoomId == room.RoomId
            || (previousRoomId.HasValue && order.All(r => r.RoomId != previousRoomId.Value)))
        {
            return;
        }

        order.Remove(room);
        var place = previousRoomId.HasValue
            ? order.FindIndex(r => r.RoomId == previousRoomId.Value) + 1
            : 0;
        order.Insert(place, room);

        for (var i = 0; i < order.Count; i++)
        {
            order[i].OrderNumber = i + 1;
        }
    }

    public async Task Delete(Guid roomId, Guid deletedByUserId)
    {
        // Two writes: the room itself and the attachments of its posts. A refusal
        // between them leaves the room gone with its files still served, or the
        // files retired under a room nobody deleted, and nothing repairs either.
        // The strategy wrapper is required because the API host configures
        // EnableRetryOnFailure.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            var room = await _dbContext.Rooms.FindAsync(roomId);
            if (room == null) return;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var now = _dateTimeProvider.Now;
            SoftDelete.Mark(room, deletedByUserId, now);

            // The attachments of this room's posts retire with it. Two reasons
            // they cannot be left for the game sweep: deleting a room does not
            // soft-delete its posts, so the orphan sweeper - which only walks
            // removed rows - never reaches these files; and the game-level sweep
            // selects its rows through the post's room, which the soft-delete
            // filter has by then hidden, so a room deleted before its game takes
            // its files out of both paths at once.
            await _dbContext.Uploads
                .Where(u => u.Type == UploadType.PostAttachment
                            && u.TargetPostId.HasValue
                            && _dbContext.Posts.Any(p => p.PostId == u.TargetPostId.Value
                                                         && p.RoomId == roomId))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.IsRemoved, true)
                    .SetProperty(x => x.DeletedByUserId, (Guid?)deletedByUserId)
                    .SetProperty(x => x.DeletedUtc, (DateTimeOffset?)now));

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }

    /// <summary>
    /// Holds the game row until the transaction ends: the writes that read the
    /// rooms of a game and write them back depending on what they read go one
    /// at a time per game.
    /// </summary>
    private Task LockGame(Guid gameId) =>
        _dbContext.Database.ExecuteSqlRawAsync(
            """SELECT "GameId" FROM "Games" WHERE "GameId" = {0} FOR UPDATE""", gameId);

    #endregion
}
