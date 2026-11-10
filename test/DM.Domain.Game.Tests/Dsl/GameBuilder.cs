using System;
using System.Linq;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Blacklists;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Tests.Dsl;

public class GameBuilder
{
    public static GameDto Bare(Guid gameId = default) => new()
    {
        Id = gameId,
        Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Master" }
    };

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

    public GameBuilder WithDraftVisibility(DraftVisibility visibility)
    {
        game.DraftVisibility = visibility;
        return this;
    }

    public GameBuilder WithAssistants(params Guid[] userIds)
    {
        game.Assistants = userIds.Select(userId => new GameAssistantInfo
        {
            UserId = userId,
            Username = $"Assistant_{userId:N}",
            JoinedUtc = DateTimeOffset.UtcNow
        }).ToList();
        return this;
    }

    public GameBuilder WithPlayers(params Guid[] userIds)
    {
        game.Players = userIds
            .Select(userId => new GeneralUser { UserId = userId, Username = $"Player_{userId:N}", Role = UserRole.RegularUser })
            .ToList();
        return this;
    }

    public GameBuilder WithViewerSubscribed(int subscribersCount = 1)
    {
        game.IsViewerSubscriber = true;
        game.SubscribersCount = subscribersCount;
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

    /// <summary>
    /// The game's owner put these users on its blacklist. It closes writing and
    /// not reading, so a game built with it still reads as the public game it is.
    /// </summary>
    public GameBuilder WithBlacklisted(params Guid[] userIds)
    {
        game.BlacklistedUsers = userIds
            .Select(userId => new BlacklistedUser { UserId = userId, LinkId = Guid.NewGuid() })
            .ToList();
        return this;
    }

    // Recruitment is what admits a character; an unset block reads as closed.
    public GameBuilder WithRecruitmentOpen()
    {
        game.Recruitment = new GameRecruitment { IsOpen = true };
        return this;
    }

    // A player invitation the user has not answered yet: the second way into character creation,
    // past a closed recruitment.
    public GameBuilder WithPendingPlayerInvitation(Guid userId)
    {
        game.PendingPlayerInvitedUserIds = new[] { userId };
        return this;
    }

    public GameDetails Please() => game;
}
