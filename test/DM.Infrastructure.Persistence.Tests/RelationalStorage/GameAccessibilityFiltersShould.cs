using System;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using FluentAssertions;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.Tests.RelationalStorage;

/// <summary>
/// The storage filters and ModuleVisibility.IsPubliclyVisible answer one
/// question about a user holding no role in the game, and the rule is written
/// twice because EF Core translates expression trees and not method calls.
/// Every combination of the three fields it reads is compared here, so a change
/// to one copy alone turns this red instead of splitting the game list from the
/// game page.
/// </summary>
public class GameAccessibilityFiltersShould
{
    private static readonly Guid Stranger = Guid.NewGuid();

    public static TheoryData<ModuleStatus, PremoderationStatus, DraftVisibility> ModuleStates()
    {
        var states = new TheoryData<ModuleStatus, PremoderationStatus, DraftVisibility>();
        foreach (var status in Enum.GetValues<ModuleStatus>())
        {
            foreach (var premoderationStatus in Enum.GetValues<PremoderationStatus>())
            {
                foreach (var draftVisibility in Enum.GetValues<DraftVisibility>())
                {
                    states.Add(status, premoderationStatus, draftVisibility);
                }
            }
        }

        return states;
    }

    [Theory]
    [MemberData(nameof(ModuleStates))]
    public void ListTheGameExactlyWhenTheDomainRuleCallsItVisible(
        ModuleStatus status, PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        var games = new[] { Game(status, premoderationStatus, draftVisibility) }.AsQueryable();

        var listed = games.Any(GameAccessibilityFilters.GameAvailable(Stranger));

        listed.Should().Be(ModuleVisibility.IsPubliclyVisible(status, premoderationStatus, draftVisibility));
    }

    [Theory]
    [MemberData(nameof(ModuleStates))]
    public void OpenTheRoomExactlyWhenTheDomainRuleCallsItsGameVisible(
        ModuleStatus status, PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        var rooms = new[] { Room(Game(status, premoderationStatus, draftVisibility)) }.AsQueryable();

        var available = rooms.Any(GameAccessibilityFilters.RoomAvailable(Stranger));

        available.Should().Be(ModuleVisibility.IsPubliclyVisible(status, premoderationStatus, draftVisibility));
    }

    // The master is somebody else, no assistants, no curator, no invitation:
    // every role arm of the filter is false, so the visibility of the module is
    // the only thing left to decide the answer
    private static DbGame Game(
        ModuleStatus status,
        PremoderationStatus premoderationStatus,
        DraftVisibility draftVisibility) => new()
    {
        GameId = Guid.NewGuid(),
        MasterId = Guid.NewGuid(),
        Status = status,
        PremoderationStatus = premoderationStatus,
        DraftVisibility = draftVisibility
    };

    // An open room, so that the room's own access type cannot decide the answer
    // either
    private static DbRoom Room(DbGame game) => new()
    {
        RoomId = Guid.NewGuid(),
        GameId = game.GameId,
        Game = game,
        AccessType = RoomAccessType.Open
    };
}
