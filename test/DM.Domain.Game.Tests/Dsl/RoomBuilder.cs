using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Tests.Dsl;

/// <summary>
/// Builds a room together with its access rows. Access is a three level nest
/// (row -> character -> author), so spelling it out inline hides the one field
/// a test is actually about.
/// </summary>
public class RoomBuilder
{
    private readonly List<RoomAccess> accesses = [];

    private readonly RoomToUpdate room = new()
    {
        Id = Guid.NewGuid(),
        RoomNumber = 1,
        Title = "Test Room",
        Type = RoomType.Default,
        Game = new GameBuilder().Please()
    };

    public RoomBuilder WithGame(GameDto game)
    {
        room.Game = game;
        return this;
    }

    public RoomBuilder WithType(RoomType type)
    {
        room.Type = type;
        return this;
    }

    /// <summary>
    /// Grants a character the right to be posted as in this room.
    /// </summary>
    public RoomBuilder WithCharacterAccess(Guid characterId, Guid authorId, bool isNpc = false)
    {
        accesses.Add(new RoomAccess
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            TargetType = RoomAccessTargetType.Character,
            Character = new Character
            {
                Id = characterId,
                IsNpc = isNpc,
                Author = new GeneralUser { UserId = authorId }
            },
            User = new GeneralUser { UserId = authorId }
        });
        return this;
    }

    /// <summary>
    /// Grants a user reader access: a spectator row with no character behind it.
    /// </summary>
    public RoomBuilder WithReaderAccess(Guid userId)
    {
        accesses.Add(new RoomAccess
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            TargetType = RoomAccessTargetType.Reader,
            User = new GeneralUser { UserId = userId }
        });
        return this;
    }

    /// <summary>
    /// A reader row that carries no user. Only for pinning down how the two
    /// overloads differ when they walk the same row.
    /// </summary>
    public RoomBuilder WithReaderAccessMissingItsUser()
    {
        accesses.Add(new RoomAccess
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            TargetType = RoomAccessTargetType.Reader,
            User = null!
        });
        return this;
    }

    public RoomToUpdate Please()
    {
        room.Accesses = accesses;
        return room;
    }
}
