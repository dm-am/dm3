using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Tests.Dsl;

public class GameBuilder
{
    private readonly GameDetails game = new()
    {
        Id = Guid.NewGuid(),
        Title = "Test Game",
        SystemName = "Test System",
        NarrativeSetting = "Test Setting",
        Status = ModuleStatus.Active,
        PremoderationStatus = PremoderationStatus.Approved,
        CommentsAccessMode = CommentsAccessMode.Public,
        Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "TestMaster", Role = UserRole.RegularUser },
        Assistants = [],
        Players = [],
        SubscriberIds = [],
        PendingInvitedUserIds = [],
        PendingPlayerInvitedUserIds = [],
        BlacklistedUsers = []
    };

    public GameBuilder WithMaster(Guid userId)
    {
        game.Master = new GeneralUser { UserId = userId, Username = "Master", Role = UserRole.RegularUser };
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
        var assistants = new List<GameAssistantInfo>();
        foreach (var userId in userIds)
        {
            assistants.Add(new GameAssistantInfo
            {
                UserId = userId,
                Username = $"Assistant_{userId:N}",
                JoinedUtc = DateTimeOffset.UtcNow
            });
        }
        game.Assistants = assistants;
        return this;
    }

    public GameBuilder WithPlayers(params Guid[] userIds)
    {
        var players = new List<GeneralUser>();
        foreach (var userId in userIds)
        {
            players.Add(new GeneralUser { UserId = userId, Username = $"Player_{userId:N}", Role = UserRole.RegularUser });
        }
        game.Players = players;
        return this;
    }

    public GameBuilder WithSubscribers(params Guid[] userIds)
    {
        game.SubscriberIds = userIds;
        return this;
    }

    public GameBuilder WithMentor(Guid userId)
    {
        game.Mentor = new GeneralUser { UserId = userId };
        return this;
    }

    public GameBuilder WithCommentsAccessMode(CommentsAccessMode mode)
    {
        game.CommentsAccessMode = mode;
        return this;
    }

    public GameDetails Please() => game;
}
