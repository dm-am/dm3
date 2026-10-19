using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Inactivity;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Core;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Inactivity;

/// <summary>
/// The four passes of the inactivity state machine. Each pass decides which
/// games it acts on and how far it moves them, so the tests capture what the
/// processor did to which game ids and assert the whole set: a pass that
/// silently skips a game, moves one two steps at once, or announces something
/// it failed to write is a wrong answer about live games.
/// </summary>
public class GameInactivityProcessorShould : UnitTestBase
{
    /// <summary>
    /// The well-known system author, spelled out rather than taken from
    /// SystemUser so that changing the constant has to be a deliberate edit here
    /// too — every warning ever posted is attributed to this id.
    /// </summary>
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IInactivityRepository inactivityRepository;
    private readonly IGameCommentRepository commentRepository;
    private readonly IUnreadCountersRepository countersRepository;
    private readonly IEventProducer eventProducer;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IGuidFactory guidFactory;
    private readonly GameInactivityProcessor processor;
    private readonly DateTimeOffset now;

    private readonly List<CreateGameCommentEntity> createdComments = new();
    private readonly List<(Guid GameId, DateTimeOffset At)> inactivityWarnings = new();
    private readonly List<(Guid GameId, DateTimeOffset At)> closureWarnings = new();
    private readonly List<(Guid GameId, DateTimeOffset At)> frozenGames = new();
    private readonly List<Guid> closedGames = new();
    private readonly List<(EventType Type, Guid GameId)> sentEvents = new();
    private readonly List<(Guid GameId, UnreadEntryType Type)> unreadIncrements = new();

    public GameInactivityProcessorShould()
    {
        inactivityRepository = Mock<IInactivityRepository>();
        commentRepository = Mock<IGameCommentRepository>();
        countersRepository = Mock<IUnreadCountersRepository>();
        eventProducer = Mock<IEventProducer>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        guidFactory = Mock<IGuidFactory>();
        var logger = Mock<ILogger<GameInactivityProcessor>>();

        now = DateTimeOffset.UtcNow;
        dateTimeProvider.Now.Returns(now);
        guidFactory.Create().Returns(Guid.NewGuid());

        // Every write the processor can make is recorded, so a test asserts the
        // set of games acted on instead of one interaction at a time. The recording
        // sits in the answer rather than in an AndDoes callback: a test that
        // reconfigures one identifier to fail replaces the answer, while callbacks
        // accumulate and would file the write that threw as if it had landed.
        commentRepository.Create(Arg.Any<CreateGameCommentEntity>())
            .Returns(ci =>
            {
                createdComments.Add(ci.Arg<CreateGameCommentEntity>());
                return new Comment { Id = Guid.NewGuid() };
            });
        inactivityRepository
            .SetInactivityWarning(Arg.Any<Guid>(), Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci => RecordStamped(inactivityWarnings, ci, at: 2));
        inactivityRepository
            .SetClosureWarning(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci => RecordStamped(closureWarnings, ci));
        inactivityRepository
            .FreezeGame(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci => RecordStamped(frozenGames, ci));
        inactivityRepository.CloseGame(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                closedGames.Add(ci.ArgAt<Guid>(0));
                return true;
            });
        countersRepository.IncrementAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>())
            .Returns(ci =>
            {
                unreadIncrements.Add((ci.ArgAt<Guid>(0), ci.ArgAt<UnreadEntryType>(1)));
                return Task.CompletedTask;
            });
        eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>())
            .Returns(ci =>
            {
                sentEvents.Add((ci.ArgAt<EventType>(0), ci.ArgAt<Guid>(1)));
                return Task.CompletedTask;
            });

        processor = new GameInactivityProcessor(
            inactivityRepository,
            commentRepository,
            countersRepository,
            eventProducer,
            dateTimeProvider,
            guidFactory,
            logger);
    }

    /// <summary>
    /// The game and the moment stamped on it. The moment sits at a different
    /// argument in the warning, which carries the threshold its cutoff is measured
    /// with, so its position is named rather than assumed.
    /// </summary>
    private static bool RecordStamped(
        List<(Guid GameId, DateTimeOffset At)> log, CallInfo call, int at = 1)
    {
        log.Add((call.ArgAt<Guid>(0), call.ArgAt<DateTimeOffset>(at)));
        return true;
    }

    [Fact]
    public async Task AskForGamesSilentForAMonthToWarn()
    {
        var thresholds = new List<TimeSpan>();
        inactivityRepository.GetInactiveGamesToWarn(
                Arg.Do<TimeSpan>(thresholds.Add), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Guid>());

        await processor.WarnInactiveGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task TouchNothingWhenNoGameIsDueAWarning()
    {
        inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());

        await processor.WarnInactiveGamesAsync(CancellationToken.None);

        // The absence of collaborator calls IS the behaviour here, so it is
        // asserted whole rather than one DidNotReceive at a time.
        await inactivityRepository.Received(1).GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        inactivityRepository.ShouldHaveReceivedNothingElse(
            nameof(IInactivityRepository.GetInactiveGamesToWarn));
        commentRepository.ShouldHaveReceivedNoCalls();
        countersRepository.ShouldHaveReceivedNoCalls();
        eventProducer.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task WarnEveryInactiveGameInTheBatchOnce()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { gameId1, gameId2 });

        await processor.WarnInactiveGamesAsync(CancellationToken.None);

        createdComments.Select(c => c.GameId).Should().Equal(gameId1, gameId2);
        createdComments.Should().OnlyContain(c => c.AuthorId == SystemUserId);
        createdComments.Should().OnlyContain(c => c.Text.StartsWith("Месяц", StringComparison.Ordinal));
        // The number the warning takes in the game's comment thread is the
        // database's answer, not a count read a query earlier and written back: a
        // comment posted between the read and the write took the same number.
        await commentRepository.DidNotReceive().Count(
            Arg.Any<Guid>(), Arg.Any<CommentsQuery>(), Arg.Any<IReadOnlyCollection<Guid>?>());
        inactivityWarnings.Should().Equal((gameId1, now), (gameId2, now));
        sentEvents.Should().Equal(
            (EventType.GameInactivityWarning, gameId1),
            (EventType.GameInactivityWarning, gameId2));
        unreadIncrements.Should().Equal(
            (gameId1, UnreadEntryType.Message),
            (gameId2, UnreadEntryType.Message));
        // One step per pass: a warned game waits out the grace period, it is not
        // frozen or closed by the same run
        frozenGames.Should().BeEmpty();
        closedGames.Should().BeEmpty();
    }

    [Fact]
    public async Task StampTheCommentWithTheMomentOfThePass()
    {
        var later = now.AddSeconds(5);
        dateTimeProvider.Now.Returns(now, later);
        inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { Guid.NewGuid() });

        await processor.WarnInactiveGamesAsync(CancellationToken.None);

        createdComments.Single().CreatedUtc.Should().Be(now);
    }

    [Fact]
    public async Task NotRecordAnInactivityWarningItFailedToPost()
    {
        var gameId = Guid.NewGuid();
        inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { gameId });
        commentRepository.Create(Arg.Any<CreateGameCommentEntity>()).ThrowsAsync(new Exception("DB error"));

        await processor.WarnInactiveGamesAsync(CancellationToken.None);

        // A recorded warning starts the week that ends in a freeze. Recording one
        // the players were never shown would freeze the game unannounced, so the
        // game must stay unwarned and be picked up by the next run.
        inactivityWarnings.Should().BeEmpty();
        sentEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task KeepWarningInactiveGamesAfterOneFails()
    {
        var failingGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { failingGameId, healthyGameId });
        commentRepository.Create(Arg.Is<CreateGameCommentEntity>(c => c.GameId == failingGameId))
            .ThrowsAsync(new Exception("DB error"));

        await processor.WarnInactiveGamesAsync(CancellationToken.None);

        createdComments.Select(c => c.GameId).Should().Equal(healthyGameId);
        inactivityWarnings.Should().Equal((healthyGameId, now));
        sentEvents.Should().Equal((EventType.GameInactivityWarning, healthyGameId));
    }

    [Fact]
    public async Task NotAnnounceAnInactivityWarningTheStampDidNotLand()
    {
        var takenGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { takenGameId, healthyGameId });
        // The batch is read once and worked through game by game: this game stopped
        // being an unwarned active one in between, so its stamp writes no row.
        inactivityRepository
            .SetInactivityWarning(takenGameId, Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await processor.WarnInactiveGamesAsync(CancellationToken.None);

        inactivityWarnings.Should().Equal((healthyGameId, now));
        sentEvents.Should().Equal((EventType.GameInactivityWarning, healthyGameId));
    }

    [Fact]
    public async Task AskForGamesWarnedAWeekAgoToFreeze()
    {
        var thresholds = new List<TimeSpan>();
        inactivityRepository.GetWarnedGamesToFreeze(
                Arg.Do<TimeSpan>(thresholds.Add), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Guid>());

        await processor.FreezeWarnedGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task TouchNothingWhenNoGameIsDueAFreeze()
    {
        inactivityRepository.GetWarnedGamesToFreeze(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());

        await processor.FreezeWarnedGamesAsync(CancellationToken.None);

        await inactivityRepository.Received(1).GetWarnedGamesToFreeze(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        inactivityRepository.ShouldHaveReceivedNothingElse(
            nameof(IInactivityRepository.GetWarnedGamesToFreeze));
        commentRepository.ShouldHaveReceivedNoCalls();
        countersRepository.ShouldHaveReceivedNoCalls();
        eventProducer.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task FreezeEveryWarnedGameInTheBatchAndAnnounceIt()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        inactivityRepository.GetWarnedGamesToFreeze(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { gameId1, gameId2 });

        await processor.FreezeWarnedGamesAsync(CancellationToken.None);

        frozenGames.Should().Equal((gameId1, now), (gameId2, now));
        sentEvents.Should().Equal(
            (EventType.StatusGameFrozen, gameId1),
            (EventType.StatusGameFrozen, gameId2));
        // The freeze itself says nothing in the game: the warning a week ago did
        createdComments.Should().BeEmpty();
        closedGames.Should().BeEmpty();
    }

    [Fact]
    public async Task NotAnnounceAGameItCouldNotFreeze()
    {
        var failingGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        inactivityRepository.GetWarnedGamesToFreeze(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { failingGameId, healthyGameId });
        inactivityRepository
            .FreezeGame(failingGameId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("DB error"));

        await processor.FreezeWarnedGamesAsync(CancellationToken.None);

        // Subscribers would otherwise be told a game is frozen while it still runs
        frozenGames.Should().Equal((healthyGameId, now));
        sentEvents.Should().Equal((EventType.StatusGameFrozen, healthyGameId));
    }

    [Fact]
    public async Task NotAnnounceAFreezeOfAGameThatCameBackToLife()
    {
        var revivedGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        inactivityRepository.GetWarnedGamesToFreeze(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { revivedGameId, healthyGameId });
        // The master posted between the selection and the write, so the freeze
        // touched no row. The pass has to believe the write, not the list it read
        // a moment earlier.
        inactivityRepository
            .FreezeGame(revivedGameId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await processor.FreezeWarnedGamesAsync(CancellationToken.None);

        frozenGames.Should().Equal((healthyGameId, now));
        sentEvents.Should().Equal((EventType.StatusGameFrozen, healthyGameId));
    }

    [Fact]
    public async Task AskForGamesFrozenForThreeMonthsToWarn()
    {
        var thresholds = new List<TimeSpan>();
        inactivityRepository.GetFrozenGamesToWarn(
                Arg.Do<TimeSpan>(thresholds.Add), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Guid>());

        await processor.WarnFrozenGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(90));
    }

    [Fact]
    public async Task TouchNothingWhenNoGameIsDueAClosureWarning()
    {
        inactivityRepository.GetFrozenGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());

        await processor.WarnFrozenGamesAsync(CancellationToken.None);

        await inactivityRepository.Received(1).GetFrozenGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        inactivityRepository.ShouldHaveReceivedNothingElse(
            nameof(IInactivityRepository.GetFrozenGamesToWarn));
        commentRepository.ShouldHaveReceivedNoCalls();
        countersRepository.ShouldHaveReceivedNoCalls();
        eventProducer.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task WarnEveryLongFrozenGameInTheBatchOnce()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        inactivityRepository.GetFrozenGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { gameId1, gameId2 });

        await processor.WarnFrozenGamesAsync(CancellationToken.None);

        createdComments.Select(c => c.GameId).Should().Equal(gameId1, gameId2);
        createdComments.Should().OnlyContain(c => c.AuthorId == SystemUserId);
        // The closure warning, not the inactivity one - the two paths differ only
        // in their message and in which timestamp they stamp
        createdComments.Should().OnlyContain(c => c.Text.StartsWith("Три месяца", StringComparison.Ordinal));
        // Same as the inactivity warning: the count belongs to the database.
        await commentRepository.DidNotReceive().Count(
            Arg.Any<Guid>(), Arg.Any<CommentsQuery>(), Arg.Any<IReadOnlyCollection<Guid>?>());
        closureWarnings.Should().Equal((gameId1, now), (gameId2, now));
        inactivityWarnings.Should().BeEmpty();
        sentEvents.Should().Equal(
            (EventType.GameClosureWarning, gameId1),
            (EventType.GameClosureWarning, gameId2));
        unreadIncrements.Should().Equal(
            (gameId1, UnreadEntryType.Message),
            (gameId2, UnreadEntryType.Message));
        // A warned frozen game gets its week; closing it here would close it
        // seven days early
        closedGames.Should().BeEmpty();
    }

    [Fact]
    public async Task KeepWarningFrozenGamesAfterOneFails()
    {
        var failingGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        inactivityRepository.GetFrozenGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { failingGameId, healthyGameId });
        commentRepository.Create(Arg.Is<CreateGameCommentEntity>(c => c.GameId == failingGameId))
            .ThrowsAsync(new Exception("DB error"));

        await processor.WarnFrozenGamesAsync(CancellationToken.None);

        createdComments.Select(c => c.GameId).Should().Equal(healthyGameId);
        closureWarnings.Should().Equal((healthyGameId, now));
        sentEvents.Should().Equal((EventType.GameClosureWarning, healthyGameId));
    }

    [Fact]
    public async Task NotAnnounceAClosureWarningTheStampDidNotLand()
    {
        var takenGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        inactivityRepository.GetFrozenGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { takenGameId, healthyGameId });
        // Restarted by the master, or warned by a second instance, since the
        // selection: the stamp wrote no row.
        inactivityRepository
            .SetClosureWarning(takenGameId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await processor.WarnFrozenGamesAsync(CancellationToken.None);

        closureWarnings.Should().Equal((healthyGameId, now));
        sentEvents.Should().Equal((EventType.GameClosureWarning, healthyGameId));
    }

    [Fact]
    public async Task AskForFrozenGamesWarnedAWeekAgoToClose()
    {
        var thresholds = new List<TimeSpan>();
        inactivityRepository
            .GetWarnedFrozenGamesToClose(
                Arg.Do<TimeSpan>(thresholds.Add), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Guid>());

        await processor.CloseFrozenGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task TouchNothingWhenNoGameIsDueAClosure()
    {
        inactivityRepository
            .GetWarnedFrozenGamesToClose(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());

        await processor.CloseFrozenGamesAsync(CancellationToken.None);

        await inactivityRepository.Received(1).GetWarnedFrozenGamesToClose(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        inactivityRepository.ShouldHaveReceivedNothingElse(
            nameof(IInactivityRepository.GetWarnedFrozenGamesToClose));
        commentRepository.ShouldHaveReceivedNoCalls();
        countersRepository.ShouldHaveReceivedNoCalls();
        eventProducer.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task CloseEveryWarnedFrozenGameInTheBatchAndAnnounceIt()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        inactivityRepository
            .GetWarnedFrozenGamesToClose(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { gameId1, gameId2 });

        await processor.CloseFrozenGamesAsync(CancellationToken.None);

        closedGames.Should().Equal(gameId1, gameId2);
        sentEvents.Should().Equal(
            (EventType.StatusGameClosed, gameId1),
            (EventType.StatusGameClosed, gameId2));
        createdComments.Should().BeEmpty();
        frozenGames.Should().BeEmpty();
    }

    [Fact]
    public async Task NotAnnounceAGameItCouldNotClose()
    {
        var failingGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        inactivityRepository
            .GetWarnedFrozenGamesToClose(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { failingGameId, healthyGameId });
        inactivityRepository.CloseGame(failingGameId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("DB error"));

        await processor.CloseFrozenGamesAsync(CancellationToken.None);

        closedGames.Should().Equal(healthyGameId);
        sentEvents.Should().Equal((EventType.StatusGameClosed, healthyGameId));
    }

    [Fact]
    public async Task NotAnnounceAClosureOfAGameThatIsNoLongerFrozen()
    {
        var restartedGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        inactivityRepository
            .GetWarnedFrozenGamesToClose(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { restartedGameId, healthyGameId });
        // Restarted between the selection and the write. The closure wrote no row,
        // and announcing one would tell subscribers a running game had been closed.
        inactivityRepository.CloseGame(restartedGameId, Arg.Any<CancellationToken>())
            .Returns(false);

        await processor.CloseFrozenGamesAsync(CancellationToken.None);

        closedGames.Should().Equal(healthyGameId);
        sentEvents.Should().Equal((EventType.StatusGameClosed, healthyGameId));
    }
}
