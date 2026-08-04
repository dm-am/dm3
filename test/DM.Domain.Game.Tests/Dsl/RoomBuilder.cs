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
    public RoomBuilder WithCharacterAccess(
        Guid characterId,
        Guid authorId,
        bool isNpc = false,
        RoomAccessPolicy policy = RoomAccessPolicy.Full)
    {
        accesses.Add(new RoomAccess
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            TargetType = RoomAccessTargetType.Character,
            Policy = policy,
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
    public RoomBuilder WithReaderAccess(Guid userId, RoomAccessPolicy policy = RoomAccessPolicy.Full)
    {
        accesses.Add(new RoomAccess
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            TargetType = RoomAccessTargetType.Reader,
            Policy = policy,
            User = new GeneralUser { UserId = userId }
        });
        return this;
    }

    /// <summary>
    /// A reader row that carries no user. The user is filled by a projection and
    /// not by a schema constraint, so an empty one is reachable and every overload
    /// that walks the row has to deny on it.
    /// </summary>
    public RoomBuilder WithReaderAccessMissingItsUser()
    {
        accesses.Add(new RoomAccess
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            TargetType = RoomAccessTargetType.Reader,
            // Granted writing on purpose: what has to deny here is the missing
            // user, and a row that also lacked the policy would deny for two
            // reasons and prove neither.
            Policy = RoomAccessPolicy.Full,
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
