using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class RoomRepository : IRoomRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    #region Read Operations

    public async Task<IEnumerable<Room>> GetAllAvailable(Guid gameId, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(r => r.GameId == gameId)
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .OrderBy(r => r.OrderNumber)
            .ProjectTo<Room>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    public Task<Room?> GetAvailable(Guid roomId, Guid userId)
    {
        return _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .ProjectTo<Room>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }

    public Task<Room?> GetByGameAndNumber(Guid gameId, int roomNumber, Guid userId)
    {
        return _dbContext.Rooms
            .Where(r => r.GameId == gameId && r.RoomNumber == roomNumber)
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .ProjectTo<Room>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }

    public Task<RoomToUpdate?> GetForUpdate(Guid roomId, Guid userId)
    {
        return _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .ProjectTo<RoomToUpdate>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }

    public Task<RoomNeighbours> GetNeighbours(Guid roomId)
    {
        return _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .ProjectTo<RoomNeighbours>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    public Task<RoomOrderInfo?> GetFirstRoomInfo(Guid gameId)
    {
        return _dbContext.Rooms
            .Where(r => !r.IsRemoved && r.GameId == gameId)
            .OrderBy(r => r.OrderNumber)
            .ProjectTo<RoomOrderInfo>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public Task<RoomOrderInfo?> GetLastRoomInfo(Guid gameId)
    {
        return _dbContext.Rooms
            .Where(r => !r.IsRemoved && r.GameId == gameId)
            .OrderByDescending(r => r.OrderNumber)
            .ProjectTo<RoomOrderInfo>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    #endregion

    #region Write Operations

    public async Task<Room> Create(CreateRoomEntity createRoom)
    {
        // Get the last room to set proper links
        var lastRoom = await _dbContext.Rooms
            .Where(r => !r.IsRemoved && r.GameId == createRoom.GameId)
            .OrderByDescending(r => r.OrderNumber)
            .FirstOrDefaultAsync();

        var dbRoom = new DbRoom
        {
            RoomId = createRoom.RoomId,
            GameId = createRoom.GameId,
            Title = createRoom.Title,
            Type = createRoom.Type,
            AccessType = createRoom.AccessType,
            ViewPrivateText = createRoom.ViewPrivateText,
            ViewDiceResults = createRoom.ViewDiceResults,
            DiceEnabled = createRoom.DiceEnabled,
            IsArchived = createRoom.IsArchived,
            OrderNumber = createRoom.OrderNumber,
            PreviousRoomId = lastRoom?.RoomId,
            IsRemoved = false
        };

        _dbContext.Rooms.Add(dbRoom);

        // Update last room's next pointer
        if (lastRoom != null)
        {
            lastRoom.NextRoomId = createRoom.RoomId;
        }

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Rooms
            .Where(r => r.RoomId == createRoom.RoomId)
            .ProjectTo<Room>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    public async Task<Room> Update(UpdateRoomEntity updateRoom)
    {
        var room = await _dbContext.Rooms.FindAsync(updateRoom.RoomId);
        if (room == null)
        {
            throw new InvalidOperationException($"Room {updateRoom.RoomId} not found");
        }

        // Update simple fields
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

        if (updateRoom.IsArchived.HasValue)
            room.IsArchived = updateRoom.IsArchived.Value;

        if (updateRoom.ShouldSetChatId)
            room.ChatId = updateRoom.ChatId;

        // Handle room reordering if requested
        if (updateRoom.ShouldReorder)
        {
            var newPreviousId = updateRoom.NewPreviousRoomId;

            // Only reorder if position actually changed
            if (room.PreviousRoomId != newPreviousId)
            {
                // Remove from current position
                if (room.PreviousRoomId.HasValue)
                {
                    var oldPrev = await _dbContext.Rooms.FindAsync(room.PreviousRoomId.Value);
                    if (oldPrev != null)
                        oldPrev.NextRoomId = room.NextRoomId;
                }

                if (room.NextRoomId.HasValue)
                {
                    var oldNext = await _dbContext.Rooms.FindAsync(room.NextRoomId.Value);
                    if (oldNext != null)
                        oldNext.PreviousRoomId = room.PreviousRoomId;
                }

                // Insert at new position
                room.PreviousRoomId = newPreviousId;

                if (newPreviousId.HasValue && newPreviousId.Value != Guid.Empty)
                {
                    var newPrev = await _dbContext.Rooms.FindAsync(newPreviousId.Value);
                    if (newPrev != null)
                    {
                        room.NextRoomId = newPrev.NextRoomId;
                        newPrev.NextRoomId = room.RoomId;
                    }

                    if (room.NextRoomId.HasValue)
                    {
                        var newNext = await _dbContext.Rooms.FindAsync(room.NextRoomId.Value);
                        if (newNext != null)
                            newNext.PreviousRoomId = room.RoomId;
                    }
                }
                else
                {
                    // Moving to first position
                    var firstRoom = await _dbContext.Rooms
                        .Where(r => !r.IsRemoved && r.GameId == room.GameId && !r.PreviousRoomId.HasValue && r.RoomId != room.RoomId)
                        .FirstOrDefaultAsync();

                    room.PreviousRoomId = null;
                    room.NextRoomId = firstRoom?.RoomId;

                    if (firstRoom != null)
                        firstRoom.PreviousRoomId = room.RoomId;
                }
            }
        }

        // Handle soft delete if requested
        if (updateRoom.IsRemoved.HasValue)
            room.IsRemoved = updateRoom.IsRemoved.Value;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Rooms
            .Where(r => r.RoomId == updateRoom.RoomId)
            .ProjectTo<Room>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    public async Task Delete(Guid roomId)
    {
        var room = await _dbContext.Rooms.FindAsync(roomId);
        if (room == null) return;

        // Remove from linked list
        if (room.PreviousRoomId.HasValue)
        {
            var prevRoom = await _dbContext.Rooms.FindAsync(room.PreviousRoomId.Value);
            if (prevRoom != null)
                prevRoom.NextRoomId = room.NextRoomId;
        }

        if (room.NextRoomId.HasValue)
        {
            var nextRoom = await _dbContext.Rooms.FindAsync(room.NextRoomId.Value);
            if (nextRoom != null)
                nextRoom.PreviousRoomId = room.PreviousRoomId;
        }

        room.IsRemoved = true;
        await _dbContext.SaveChangesAsync();
    }

    #endregion
}
