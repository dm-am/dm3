using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.GameModel;

namespace DM.Domain.Game.Tests.Dsl;

public class GameBuilder
{
    private readonly GameDto game = new()
    {
        Id = Guid.NewGuid(),
        Title = "Test Game",
        SystemName = "Test System",
        NarrativeSetting = "Test Setting",
        Status = ModuleStatus.Active,
        PremoderationStatus = PremoderationStatus.Approved,
        CommentsAccessMode = CommentsAccessMode.Public,
        Author = new GeneralUser { UserId = Guid.NewGuid(), Username = "TestMaster", Role = UserRole.RegularUser },
        Assistants = [],
        ActiveCharacterUserIds = [],
        ReaderUserIds = [],
        PendingInvitedUserIds = [],
        PendingPlayerInvitedUserIds = [],
        BlacklistedUsers = []
    };

    public GameBuilder WithAuthor(Guid userId)
    {
        game.Author = new GeneralUser { UserId = userId, Username = "Master", Role = UserRole.RegularUser };
        return this;
    }

    public GameBuilder WithStatus(ModuleStatus status)
    {
        game.Status = status;
        return this;
    }

    public GameBuilder WithPremoderationStatus(PremoderationStatus status)
    {
        game.PremoderationStatus = status;
        return this;
    }

    public GameBuilder WithAssistants(params Guid[] userIds)
    {
        var assistants = new List<GeneralUser>();
        foreach (var userId in userIds)
        {
            assistants.Add(new GeneralUser { UserId = userId, Username = $"Assistant_{userId:N}", Role = UserRole.RegularUser });
        }
        game.Assistants = assistants;
        return this;
    }

    public GameBuilder WithPlayers(params Guid[] userIds)
    {
        game.ActiveCharacterUserIds = userIds;
        return this;
    }

    public GameBuilder WithReaders(params Guid[] userIds)
    {
        game.ReaderUserIds = userIds;
        return this;
    }

    public GameBuilder WithCommentsAccessMode(CommentsAccessMode mode)
    {
        game.CommentsAccessMode = mode;
        return this;
    }

    public GameDto Please() => game;
}
