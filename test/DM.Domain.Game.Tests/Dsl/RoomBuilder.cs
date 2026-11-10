using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Domain.Game.Features.Rooms;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Tests.Dsl;

// Access is a three level nest (row -> character -> author), so spelling it out inline hides the
// one field a test is actually about.
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

    public RoomBuilder WithAccessType(RoomAccessType accessType)
    {
        room.AccessType = accessType;
        return this;
    }

    // The character is in play unless the case says otherwise.
    public RoomBuilder WithCharacterAccess(
        Guid characterId,
        Guid authorId,
        bool isNpc = false,
        RoomAccessPolicy policy = RoomAccessPolicy.Full,
        CharacterStatus status = CharacterStatus.Active)
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
                Status = status,
                Author = new GeneralUser { UserId = authorId }
            },
            User = new GeneralUser { UserId = authorId }
        });
        return this;
    }

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

    // The user is filled by a projection and not by a schema constraint, so an empty one is
    // reachable and every overload that walks the row has to deny on it.
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
