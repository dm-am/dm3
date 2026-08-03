using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.RelationalStorage;
using FluentAssertions;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;

namespace DM.Infrastructure.Persistence.Tests.RelationalStorage;

/// <summary>
/// The storage filters and ModuleVisibility.IsPubliclyVisible answer one
/// question about a user holding no role in the game, and the rule is written
/// twice because EF Core translates expression trees and not method calls.
/// Every combination of the three fields it reads is compared here, so a change
/// to one copy alone turns this red instead of splitting the game list from the
/// game page.
///
/// The room filter answers a second question with the same rule: whether a room
/// may be NAMED in the game menu, which a closed room may until its master takes
/// it off. That switch has to cut the room out of the query, so it is checked
/// here and not against the markup of the menu.
/// </summary>
public class GameAccessibilityFiltersShould
{
    private static readonly Guid Stranger = Guid.NewGuid();

    /// <summary>The master of the game, who holds no room grant of his own.</summary>
    private static readonly Guid Master = Guid.NewGuid();

    /// <summary>A member the closed room was granted to.</summary>
    private static readonly Guid Reader = Guid.NewGuid();

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

    /// <summary>
    /// A closed room is named to everybody by default: closing it is most often
    /// technical rather than a secret, and the name alone gives nothing away.
    /// Its master may take it off the menu, and then the room has to leave the
    /// query itself, or the switch is a decoration over a payload that still
    /// carries the room.
    /// </summary>
    [Fact]
    public void NameAClosedRoomToAStrangerUntilItsMasterHidesIt()
    {
        var game = VisibleGame();

        Listed(ClosedRoom(game, hidden: false), Stranger).Should().BeTrue();
        Listed(ClosedRoom(game, hidden: true), Stranger).Should().BeFalse();
    }

    /// <summary>
    /// The switch hides the room from those it was already closed to and from
    /// nobody else: whoever may open it still finds it in the menu, the master
    /// included, who is admitted by his role and not by a grant.
    /// </summary>
    [Fact]
    public void KeepAHiddenRoomListedForEveryoneWhoMayOpenIt()
    {
        var game = VisibleGame();

        Listed(ClosedRoom(game, hidden: true, grantedTo: Reader), Reader).Should().BeTrue();
        Listed(ClosedRoom(game, hidden: true), Master).Should().BeTrue();
    }

    [Fact]
    public void ListAPublicGameForTheUserItsOwnerBlacklisted()
    {
        var games = new[]
        {
            Game(ModuleStatus.Active, PremoderationStatus.Approved, DraftVisibility.Public, Stranger)
        }.AsQueryable();

        var listed = games.Any(GameAccessibilityFilters.GameAvailable(Stranger));

        // The blacklist closes writing and not reading. Dropping the row here hid
        // the game from the list, from search and from its own address, while
        // GameIntentionResolver.Read would have opened the page: one rule, two
        // answers. The game is public to everybody else, so the concealment
        // promised a privacy it never had.
        listed.Should().BeTrue();
    }

    [Fact]
    public void OpenAnOpenRoomForTheUserItsGameBlacklisted()
    {
        var rooms = new[]
        {
            Room(Game(ModuleStatus.Active, PremoderationStatus.Approved, DraftVisibility.Public, Stranger))
        }.AsQueryable();

        var available = rooms.Any(GameAccessibilityFilters.RoomAvailable(Stranger));

        // The rooms, their posts and the message search read by the same rule as
        // the game itself
        available.Should().BeTrue();
    }

    // The master is somebody else, no assistants, no curator, no invitation:
    // every role arm of the filter is false, so the visibility of the module is
    // the only thing left to decide the answer
    private static DbGame Game(
        ModuleStatus status,
        PremoderationStatus premoderationStatus,
        DraftVisibility draftVisibility,
        Guid? blacklisted = null) => new()
    {
        GameId = Guid.NewGuid(),
        MasterId = Guid.NewGuid(),
        Status = status,
        PremoderationStatus = premoderationStatus,
        DraftVisibility = draftVisibility,
        BlackList = blacklisted is null
            ? new List<GameBlacklist>()
            : new List<GameBlacklist> { new() { BlockedUserId = blacklisted.Value } }
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

    /// <summary>Whether the game's room list may name the room to the user.</summary>
    private static bool Listed(DbRoom room, Guid userId) => new[] { room }
        .AsQueryable()
        .Any(GameAccessibilityFilters.RoomAvailable(userId, listingOnly: true));

    // Approved and published, so the game the room belongs to admits everybody
    // and the room's own settings are the only thing left to decide the answer
    private static DbGame VisibleGame() => new()
    {
        GameId = Guid.NewGuid(),
        MasterId = Master,
        Status = ModuleStatus.Active,
        PremoderationStatus = PremoderationStatus.Approved
    };

    /// <summary>A private room, granted to at most one reader.</summary>
    private static DbRoom ClosedRoom(DbGame game, bool hidden, Guid? grantedTo = null)
    {
        var room = new DbRoom
        {
            RoomId = Guid.NewGuid(),
            GameId = game.GameId,
            Game = game,
            AccessType = RoomAccessType.Private,
            HiddenWithoutAccess = hidden
        };

        if (grantedTo.HasValue)
        {
            room.RoomAccesses.Add(new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                RoomId = room.RoomId,
                ReaderUserId = grantedTo.Value,
                Policy = RoomAccessPolicy.ReadOnly
            });
        }

        return room;
    }
}
