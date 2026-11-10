using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.RelationalStorage;
using AwesomeAssertions;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;

namespace DM.Infrastructure.Persistence.Tests.RelationalStorage;

// The storage filters and ModuleVisibility.IsPubliclyVisible answer one question twice, because EF
// translates expression trees and not method calls; every combination is compared.
public class GameAccessibilityFiltersShould
{
    private static readonly Guid Stranger = Guid.NewGuid();

    private static readonly Guid Master = Guid.NewGuid();

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

    [Fact]
    public void NameAClosedRoomToAStrangerUntilItsMasterHidesIt()
    {
        var game = VisibleGame();

        Listed(ClosedRoom(game, hidden: false), Stranger).Should().BeTrue();
        Listed(ClosedRoom(game, hidden: true), Stranger).Should().BeFalse();
    }

    // The switch hides the room from those it was already closed to and from nobody else: whoever
    // may open it still finds it in the menu, the master included, who is admitted by his role and
    // not by a grant.
    [Fact]
    public void KeepAHiddenRoomListedForEveryoneWhoMayOpenIt()
    {
        var game = VisibleGame();

        Listed(ClosedRoom(game, hidden: true, grantedTo: Reader), Reader).Should().BeTrue();
        Listed(ClosedRoom(game, hidden: true), Master).Should().BeTrue();
    }

    // The row has to reach the reader before any gate can be asked about it.
    [Theory]
    [MemberData(nameof(ModuleStates))]
    public void ListTheGameToAJudgeExactlyWhenItAwaitsAPremoderationVerdict(
        ModuleStatus status, PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        var games = new[] { Game(status, premoderationStatus, draftVisibility) }.AsQueryable();

        var listed = games.Any(GameAccessibilityFilters.GameAvailable(
            Stranger, mayJudgePremoderation: true));

        listed.Should().Be(
            PremoderationAccess.IsPending(premoderationStatus) ||
            ModuleVisibility.IsPubliclyVisible(status, premoderationStatus, draftVisibility));
    }

    // A private draft is hidden by its author, not by premoderation, so the rank does not reach it.
    [Fact]
    public void KeepAPrivateDraftShutToAJudge()
    {
        var draft = new[]
        {
            Game(ModuleStatus.Draft, PremoderationStatus.Approved, DraftVisibility.Private)
        }.AsQueryable();

        draft.Any(GameAccessibilityFilters.GameAvailable(Stranger, mayJudgePremoderation: true))
            .Should().BeFalse();
    }

    // A removed game is gone for everybody, whatever it was awaiting when it went: the soft-delete
    // flag guards the whole filter and no arm reopens it.
    [Theory]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.Approved)]
    public void KeepARemovedGameShutToAJudge(PremoderationStatus premoderationStatus)
    {
        var game = Game(ModuleStatus.Active, premoderationStatus, DraftVisibility.Public);
        game.IsRemoved = true;

        new[] { game }.AsQueryable()
            .Any(GameAccessibilityFilters.GameAvailable(Stranger, mayJudgePremoderation: true))
            .Should().BeFalse();
    }

    /// <summary>
    /// The rank is not the default: a reader who does not hold it reads exactly as
    /// before, and the list paths that pass nothing keep answering the public rule
    /// alone.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModuleStates))]
    public void AnswerTheSameWithoutTheRankAsWithTheParameterOmitted(
        ModuleStatus status, PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        var games = new[] { Game(status, premoderationStatus, draftVisibility) }.AsQueryable();

        games.Any(GameAccessibilityFilters.GameAvailable(Stranger, mayJudgePremoderation: false))
            .Should().Be(games.Any(GameAccessibilityFilters.GameAvailable(Stranger)));
    }

    /// <summary>
    /// The rooms answer the rank as the game does: the judge of a game gets its rooms to read.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModuleStates))]
    public void OpenTheRoomToAJudgeExactlyWhenItsGameAwaitsAPremoderationVerdict(
        ModuleStatus status, PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        var rooms = new[] { Room(Game(status, premoderationStatus, draftVisibility)) }.AsQueryable();

        var available = rooms.Any(GameAccessibilityFilters.RoomAvailable(
            Stranger, mayJudgePremoderation: true));

        available.Should().Be(
            PremoderationAccess.IsPending(premoderationStatus) ||
            ModuleVisibility.IsPubliclyVisible(status, premoderationStatus, draftVisibility));
    }

    [Theory]
    [MemberData(nameof(ModuleStates))]
    public void ListTheRoomToAJudgeExactlyWhenItsGameAwaitsAPremoderationVerdict(
        ModuleStatus status, PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        var rooms = new[] { Room(Game(status, premoderationStatus, draftVisibility)) }.AsQueryable();

        var listed = rooms.Any(GameAccessibilityFilters.RoomAvailable(
            Stranger, listingOnly: true, mayJudgePremoderation: true));

        listed.Should().Be(
            PremoderationAccess.IsPending(premoderationStatus) ||
            ModuleVisibility.IsPubliclyVisible(status, premoderationStatus, draftVisibility));
    }

    // A private draft is its author's own concealment and not premoderation's, so its rooms stay
    // shut to the rank exactly as the game does.
    [Fact]
    public void KeepTheRoomsOfAPrivateDraftShutToAJudge()
    {
        var rooms = new[]
        {
            Room(Game(ModuleStatus.Draft, PremoderationStatus.Approved, DraftVisibility.Private))
        }.AsQueryable();

        rooms.Any(GameAccessibilityFilters.RoomAvailable(Stranger, mayJudgePremoderation: true))
            .Should().BeFalse();
    }

    // A removed game takes its rooms with it, and a removed room stays removed inside a game still
    // awaiting a verdict: both flags guard the whole filter and the rank reopens neither.
    [Theory]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.Approved)]
    public void KeepRemovedRoomsShutToAJudge(PremoderationStatus premoderationStatus)
    {
        var removedGame = Game(ModuleStatus.Active, premoderationStatus, DraftVisibility.Public);
        removedGame.IsRemoved = true;

        var removedRoom = Room(Game(ModuleStatus.Active, premoderationStatus, DraftVisibility.Public));
        removedRoom.IsRemoved = true;

        new[] { Room(removedGame) }.AsQueryable()
            .Any(GameAccessibilityFilters.RoomAvailable(Stranger, mayJudgePremoderation: true))
            .Should().BeFalse();
        new[] { removedRoom }.AsQueryable()
            .Any(GameAccessibilityFilters.RoomAvailable(Stranger, mayJudgePremoderation: true))
            .Should().BeFalse();
    }

    // The rank answers for the game and not for the room: a closed room the judge holds no grant to
    // is named to them and stays shut, the way it is for every other reader of the game.
    [Fact]
    public void KeepAClosedRoomOfAJudgedGameNamedButShut()
    {
        var game = Game(ModuleStatus.Active, PremoderationStatus.AwaitingApproval, DraftVisibility.Public);
        var room = ClosedRoom(game, hidden: false);

        new[] { room }.AsQueryable()
            .Any(GameAccessibilityFilters.RoomAvailable(
                Stranger, listingOnly: true, mayJudgePremoderation: true))
            .Should().BeTrue();
        new[] { room }.AsQueryable()
            .Any(GameAccessibilityFilters.RoomAvailable(Stranger, mayJudgePremoderation: true))
            .Should().BeFalse();
    }

    /// <summary>
    /// The rank is not the default for the rooms either: every read that passes
    /// nothing — the pulse, search, first-unread — answers exactly as before.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModuleStates))]
    public void AnswerTheSameForRoomsWithoutTheRankAsWithTheParameterOmitted(
        ModuleStatus status, PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        var rooms = new[] { Room(Game(status, premoderationStatus, draftVisibility)) }.AsQueryable();

        rooms.Any(GameAccessibilityFilters.RoomAvailable(Stranger, mayJudgePremoderation: false))
            .Should().Be(rooms.Any(GameAccessibilityFilters.RoomAvailable(Stranger)));
        rooms.Any(GameAccessibilityFilters.RoomAvailable(
                Stranger, listingOnly: true, mayJudgePremoderation: false))
            .Should().Be(rooms.Any(GameAccessibilityFilters.RoomAvailable(Stranger, listingOnly: true)));
    }

    [Fact]
    public void ListAPublicGameForTheUserItsOwnerBlacklisted()
    {
        var games = new[]
        {
            Game(ModuleStatus.Active, PremoderationStatus.Approved, DraftVisibility.Public, Stranger)
        }.AsQueryable();

        var listed = games.Any(GameAccessibilityFilters.GameAvailable(Stranger));

        // The blacklist closes writing, not reading: GameIntentionResolver.Read opens the page.
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
            Blacklist = blacklisted is null
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
