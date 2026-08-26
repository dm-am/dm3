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
using DM.Domain.Game.Features.Games;
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

    private readonly IInactivityRepository _inactivityRepository;
    private readonly IGameCommentRepository _commentRepository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _eventProducer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly GameInactivityProcessor _processor;
    private readonly DateTimeOffset _now;

    private readonly List<CreateGameCommentEntity> _createdComments = new();
    private readonly List<(Guid GameId, DateTimeOffset At)> _inactivityWarnings = new();
    private readonly List<(Guid GameId, DateTimeOffset At)> _closureWarnings = new();
    private readonly List<(Guid GameId, DateTimeOffset At)> _frozenGames = new();
    private readonly List<Guid> _closedGames = new();
    private readonly List<(EventType Type, Guid GameId)> _sentEvents = new();
    private readonly List<(Guid GameId, UnreadEntryType Type)> _unreadIncrements = new();

    public GameInactivityProcessorShould()
    {
        _inactivityRepository = Mock<IInactivityRepository>();
        _commentRepository = Mock<IGameCommentRepository>();
        _countersRepository = Mock<IUnreadCountersRepository>();
        _eventProducer = Mock<IEventProducer>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _guidFactory = Mock<IGuidFactory>();
        var logger = Mock<ILogger<GameInactivityProcessor>>();

        _now = DateTimeOffset.UtcNow;
        _dateTimeProvider.Now.Returns(_now);
        _guidFactory.Create().Returns(Guid.NewGuid());

        // Every write the processor can make is recorded, so a test asserts the
        // set of games acted on instead of one interaction at a time. The recording
        // sits in the answer rather than in an AndDoes callback: a test that
        // reconfigures one identifier to fail replaces the answer, while callbacks
        // accumulate and would file the write that threw as if it had landed.
        _commentRepository
            .Count(Arg.Any<Guid>(), Arg.Any<CommentsQuery>(), Arg.Any<IReadOnlyCollection<Guid>?>()).Returns(0);
        _commentRepository.Create(Arg.Any<CreateGameCommentEntity>())
            .Returns(ci =>
            {
                _createdComments.Add(ci.Arg<CreateGameCommentEntity>());
                return new Comment { Id = Guid.NewGuid() };
            });
        _inactivityRepository
            .SetInactivityWarning(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci => RecordStamped(_inactivityWarnings, ci));
        _inactivityRepository
            .SetClosureWarning(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci => RecordStamped(_closureWarnings, ci));
        _inactivityRepository
            .FreezeGame(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci => RecordStamped(_frozenGames, ci));
        _inactivityRepository.CloseGame(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _closedGames.Add(ci.ArgAt<Guid>(0));
                return Task.CompletedTask;
            });
        _countersRepository.IncrementAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>())
            .Returns(ci =>
            {
                _unreadIncrements.Add((ci.ArgAt<Guid>(0), ci.ArgAt<UnreadEntryType>(1)));
                return Task.CompletedTask;
            });
        _eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>())
            .Returns(ci =>
            {
                _sentEvents.Add((ci.ArgAt<EventType>(0), ci.ArgAt<Guid>(1)));
                return Task.CompletedTask;
            });

        _processor = new GameInactivityProcessor(
            _inactivityRepository,
            _commentRepository,
            _countersRepository,
            _eventProducer,
            _dateTimeProvider,
            _guidFactory,
            logger);
    }

    /// <summary>Files the game and the moment a stamped write carried, and answers it as done.</summary>
    private static Task RecordStamped(List<(Guid GameId, DateTimeOffset At)> log, CallInfo call)
    {
        log.Add((call.ArgAt<Guid>(0), call.ArgAt<DateTimeOffset>(1)));
        return Task.CompletedTask;
    }

    #region WarnInactiveGamesAsync

    [Fact]
    public async Task WarnInactiveGames_AskForGamesSilentForAMonth()
    {
        var thresholds = new List<TimeSpan>();
        _inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>()).AndDoes(ci => { var threshold = ci.ArgAt<TimeSpan>(0); thresholds.Add(threshold); });

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task WarnInactiveGames_WhenNoGamesFound_TouchNothing()
    {
        _inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        // The absence of collaborator calls IS the behaviour here, so it is
        // asserted whole rather than one DidNotReceive at a time.
        await _inactivityRepository.Received(1).GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        _inactivityRepository.ShouldHaveReceivedNothingElse(
            nameof(IInactivityRepository.GetInactiveGamesToWarn));
        _commentRepository.ShouldHaveReceivedNoCalls();
        _countersRepository.ShouldHaveReceivedNoCalls();
        _eventProducer.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task WarnInactiveGames_WarnEveryGameInTheBatchOnce()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { gameId1, gameId2 });
        _commentRepository
            .Count(Arg.Any<Guid>(), Arg.Any<CommentsQuery>(), Arg.Any<IReadOnlyCollection<Guid>?>()).Returns(5);

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        _createdComments.Select(c => c.GameId).Should().Equal(gameId1, gameId2);
        _createdComments.Should().OnlyContain(c => c.AuthorId == SystemUserId);
        _createdComments.Should().OnlyContain(c => c.Text.StartsWith("Месяц", StringComparison.Ordinal));
        // The warning takes the next number in the game's comment thread
        _createdComments.Should().OnlyContain(c => c.NewCommentCount == 6);
        _inactivityWarnings.Should().Equal((gameId1, _now), (gameId2, _now));
        _sentEvents.Should().Equal(
            (EventType.GameInactivityWarning, gameId1),
            (EventType.GameInactivityWarning, gameId2));
        _unreadIncrements.Should().Equal(
            (gameId1, UnreadEntryType.Message),
            (gameId2, UnreadEntryType.Message));
        // One step per pass: a warned game waits out the grace period, it is not
        // frozen or closed by the same run
        _frozenGames.Should().BeEmpty();
        _closedGames.Should().BeEmpty();
    }

    [Fact]
    public async Task WarnInactiveGames_NotRecordAWarningItFailedToPost()
    {
        var gameId = Guid.NewGuid();
        _inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { gameId });
        _commentRepository.Create(Arg.Any<CreateGameCommentEntity>()).ThrowsAsync(new Exception("DB error"));

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        // A recorded warning starts the week that ends in a freeze. Recording one
        // the players were never shown would freeze the game unannounced, so the
        // game must stay unwarned and be picked up by the next run.
        _inactivityWarnings.Should().BeEmpty();
        _sentEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task WarnInactiveGames_KeepProcessingAfterOneGameFails()
    {
        var failingGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        _inactivityRepository.GetInactiveGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { failingGameId, healthyGameId });
        _commentRepository
            .Count(failingGameId, Arg.Any<CommentsQuery>(), Arg.Any<IReadOnlyCollection<Guid>?>())
            .ThrowsAsync(new Exception("DB error"));

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        _createdComments.Select(c => c.GameId).Should().Equal(healthyGameId);
        _inactivityWarnings.Should().Equal((healthyGameId, _now));
        _sentEvents.Should().Equal((EventType.GameInactivityWarning, healthyGameId));
    }

    #endregion

    #region FreezeWarnedGamesAsync

    [Fact]
    public async Task FreezeWarnedGames_AskForGamesWarnedAWeekAgo()
    {
        var thresholds = new List<TimeSpan>();
        _inactivityRepository.GetWarnedGamesToFreeze(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>()).AndDoes(ci => { var threshold = ci.ArgAt<TimeSpan>(0); thresholds.Add(threshold); });

        await _processor.FreezeWarnedGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task FreezeWarnedGames_WhenNoGamesFound_TouchNothing()
    {
        _inactivityRepository.GetWarnedGamesToFreeze(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());

        await _processor.FreezeWarnedGamesAsync(CancellationToken.None);

        await _inactivityRepository.Received(1).GetWarnedGamesToFreeze(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        _inactivityRepository.ShouldHaveReceivedNothingElse(
            nameof(IInactivityRepository.GetWarnedGamesToFreeze));
        _commentRepository.ShouldHaveReceivedNoCalls();
        _countersRepository.ShouldHaveReceivedNoCalls();
        _eventProducer.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task FreezeWarnedGames_FreezeEveryGameInTheBatchAndAnnounceIt()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.GetWarnedGamesToFreeze(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { gameId1, gameId2 });

        await _processor.FreezeWarnedGamesAsync(CancellationToken.None);

        _frozenGames.Should().Equal((gameId1, _now), (gameId2, _now));
        _sentEvents.Should().Equal(
            (EventType.StatusGameFrozen, gameId1),
            (EventType.StatusGameFrozen, gameId2));
        // The freeze itself says nothing in the game: the warning a week ago did
        _createdComments.Should().BeEmpty();
        _closedGames.Should().BeEmpty();
    }

    [Fact]
    public async Task FreezeWarnedGames_NotAnnounceAGameItCouldNotFreeze()
    {
        var failingGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        _inactivityRepository.GetWarnedGamesToFreeze(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { failingGameId, healthyGameId });
        _inactivityRepository
            .FreezeGame(failingGameId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("DB error"));

        await _processor.FreezeWarnedGamesAsync(CancellationToken.None);

        // Subscribers would otherwise be told a game is frozen while it still runs
        _frozenGames.Should().Equal((healthyGameId, _now));
        _sentEvents.Should().Equal((EventType.StatusGameFrozen, healthyGameId));
    }

    #endregion

    #region WarnFrozenGamesAsync

    [Fact]
    public async Task WarnFrozenGames_AskForGamesFrozenForThreeMonths()
    {
        var thresholds = new List<TimeSpan>();
        _inactivityRepository.GetFrozenGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>()).AndDoes(ci => { var threshold = ci.ArgAt<TimeSpan>(0); thresholds.Add(threshold); });

        await _processor.WarnFrozenGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(90));
    }

    [Fact]
    public async Task WarnFrozenGames_WhenNoGamesFound_TouchNothing()
    {
        _inactivityRepository.GetFrozenGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());

        await _processor.WarnFrozenGamesAsync(CancellationToken.None);

        await _inactivityRepository.Received(1).GetFrozenGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        _inactivityRepository.ShouldHaveReceivedNothingElse(
            nameof(IInactivityRepository.GetFrozenGamesToWarn));
        _commentRepository.ShouldHaveReceivedNoCalls();
        _countersRepository.ShouldHaveReceivedNoCalls();
        _eventProducer.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task WarnFrozenGames_WarnEveryGameInTheBatchOnce()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.GetFrozenGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { gameId1, gameId2 });
        _commentRepository
            .Count(Arg.Any<Guid>(), Arg.Any<CommentsQuery>(), Arg.Any<IReadOnlyCollection<Guid>?>()).Returns(10);

        await _processor.WarnFrozenGamesAsync(CancellationToken.None);

        _createdComments.Select(c => c.GameId).Should().Equal(gameId1, gameId2);
        _createdComments.Should().OnlyContain(c => c.AuthorId == SystemUserId);
        // The closure warning, not the inactivity one - the two paths differ only
        // in their message and in which timestamp they stamp
        _createdComments.Should().OnlyContain(c => c.Text.StartsWith("Три месяца", StringComparison.Ordinal));
        _createdComments.Should().OnlyContain(c => c.NewCommentCount == 11);
        _closureWarnings.Should().Equal((gameId1, _now), (gameId2, _now));
        _inactivityWarnings.Should().BeEmpty();
        _sentEvents.Should().Equal(
            (EventType.GameClosureWarning, gameId1),
            (EventType.GameClosureWarning, gameId2));
        _unreadIncrements.Should().Equal(
            (gameId1, UnreadEntryType.Message),
            (gameId2, UnreadEntryType.Message));
        // A warned frozen game gets its week; closing it here would close it
        // seven days early
        _closedGames.Should().BeEmpty();
    }

    [Fact]
    public async Task WarnFrozenGames_KeepProcessingAfterOneGameFails()
    {
        var failingGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        _inactivityRepository.GetFrozenGamesToWarn(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { failingGameId, healthyGameId });
        _commentRepository
            .Count(failingGameId, Arg.Any<CommentsQuery>(), Arg.Any<IReadOnlyCollection<Guid>?>())
            .ThrowsAsync(new Exception("DB error"));

        await _processor.WarnFrozenGamesAsync(CancellationToken.None);

        _createdComments.Select(c => c.GameId).Should().Equal(healthyGameId);
        _closureWarnings.Should().Equal((healthyGameId, _now));
        _sentEvents.Should().Equal((EventType.GameClosureWarning, healthyGameId));
    }

    #endregion

    #region CloseFrozenGamesAsync

    [Fact]
    public async Task CloseFrozenGames_AskForGamesWarnedAWeekAgo()
    {
        var thresholds = new List<TimeSpan>();
        _inactivityRepository
            .GetWarnedFrozenGamesToClose(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>()).AndDoes(ci => { var threshold = ci.ArgAt<TimeSpan>(0); thresholds.Add(threshold); });

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task CloseFrozenGames_WhenNoGamesFound_TouchNothing()
    {
        _inactivityRepository
            .GetWarnedFrozenGamesToClose(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        await _inactivityRepository.Received(1).GetWarnedFrozenGamesToClose(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        _inactivityRepository.ShouldHaveReceivedNothingElse(
            nameof(IInactivityRepository.GetWarnedFrozenGamesToClose));
        _commentRepository.ShouldHaveReceivedNoCalls();
        _countersRepository.ShouldHaveReceivedNoCalls();
        _eventProducer.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task CloseFrozenGames_CloseEveryGameInTheBatchAndAnnounceIt()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository
            .GetWarnedFrozenGamesToClose(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { gameId1, gameId2 });

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        _closedGames.Should().Equal(gameId1, gameId2);
        _sentEvents.Should().Equal(
            (EventType.StatusGameClosed, gameId1),
            (EventType.StatusGameClosed, gameId2));
        _createdComments.Should().BeEmpty();
        _frozenGames.Should().BeEmpty();
    }

    [Fact]
    public async Task CloseFrozenGames_NotAnnounceAGameItCouldNotClose()
    {
        var failingGameId = Guid.NewGuid();
        var healthyGameId = Guid.NewGuid();
        _inactivityRepository
            .GetWarnedFrozenGamesToClose(
                Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new[] { failingGameId, healthyGameId });
        _inactivityRepository.CloseGame(failingGameId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("DB error"));

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        _closedGames.Should().Equal(healthyGameId);
        _sentEvents.Should().Equal((EventType.StatusGameClosed, healthyGameId));
    }

    #endregion
}
