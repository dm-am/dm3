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
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
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

    private readonly Mock<IInactivityRepository> _inactivityRepository;
    private readonly Mock<IGameCommentRepository> _commentRepository;
    private readonly Mock<IUnreadCountersRepository> _countersRepository;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
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
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        // Every write the processor can make is recorded, so a test asserts the
        // set of games acted on instead of one interaction at a time.
        _commentRepository
            .Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>()))
            .ReturnsAsync(0);
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .Callback<CreateGameCommentEntity>(_createdComments.Add)
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });
        _inactivityRepository
            .Setup(r => r.SetInactivityWarning(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTimeOffset, CancellationToken>((id, at, _) => _inactivityWarnings.Add((id, at)))
            .Returns(Task.CompletedTask);
        _inactivityRepository
            .Setup(r => r.SetClosureWarning(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTimeOffset, CancellationToken>((id, at, _) => _closureWarnings.Add((id, at)))
            .Returns(Task.CompletedTask);
        _inactivityRepository
            .Setup(r => r.FreezeGame(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTimeOffset, CancellationToken>((id, at, _) => _frozenGames.Add((id, at)))
            .Returns(Task.CompletedTask);
        _inactivityRepository.Setup(r => r.CloseGame(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) => _closedGames.Add(id))
            .Returns(Task.CompletedTask);
        _countersRepository.Setup(r => r.IncrementAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Callback<Guid, UnreadEntryType>((id, type) => _unreadIncrements.Add((id, type)))
            .Returns(Task.CompletedTask);
        _eventProducer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Callback<EventType, Guid>((type, id) => _sentEvents.Add((type, id)))
            .Returns(Task.CompletedTask);

        _processor = new GameInactivityProcessor(
            _inactivityRepository.Object,
            _commentRepository.Object,
            _countersRepository.Object,
            _eventProducer.Object,
            _dateTimeProvider.Object,
            _guidFactory.Object,
            logger.Object);
    }

    #region WarnInactiveGamesAsync

    [Fact]
    public async Task WarnInactiveGames_AskForGamesSilentForAMonth()
    {
        var thresholds = new List<TimeSpan>();
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((threshold, _) => thresholds.Add(threshold))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task WarnInactiveGames_WhenNoGamesFound_TouchNothing()
    {
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        // The absence of collaborator calls IS the behaviour here, so it is
        // asserted whole rather than one Times.Never at a time.
        _inactivityRepository.Verify(
            r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _inactivityRepository.VerifyNoOtherCalls();
        _commentRepository.VerifyNoOtherCalls();
        _countersRepository.VerifyNoOtherCalls();
        _eventProducer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WarnInactiveGames_WarnEveryGameInTheBatchOnce()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });
        _commentRepository
            .Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>()))
            .ReturnsAsync(5);

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
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ThrowsAsync(new Exception("DB error"));

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
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { failingGameId, healthyGameId });
        _commentRepository
            .Setup(r => r.Count(failingGameId, It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>()))
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
        _inactivityRepository.Setup(r => r.GetWarnedGamesToFreeze(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((threshold, _) => thresholds.Add(threshold))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.FreezeWarnedGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task FreezeWarnedGames_WhenNoGamesFound_TouchNothing()
    {
        _inactivityRepository.Setup(r => r.GetWarnedGamesToFreeze(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.FreezeWarnedGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(
            r => r.GetWarnedGamesToFreeze(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _inactivityRepository.VerifyNoOtherCalls();
        _commentRepository.VerifyNoOtherCalls();
        _countersRepository.VerifyNoOtherCalls();
        _eventProducer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FreezeWarnedGames_FreezeEveryGameInTheBatchAndAnnounceIt()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetWarnedGamesToFreeze(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });

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
        _inactivityRepository.Setup(r => r.GetWarnedGamesToFreeze(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { failingGameId, healthyGameId });
        _inactivityRepository
            .Setup(r => r.FreezeGame(failingGameId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
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
        _inactivityRepository.Setup(r => r.GetFrozenGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((threshold, _) => thresholds.Add(threshold))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.WarnFrozenGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(90));
    }

    [Fact]
    public async Task WarnFrozenGames_WhenNoGamesFound_TouchNothing()
    {
        _inactivityRepository.Setup(r => r.GetFrozenGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.WarnFrozenGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(
            r => r.GetFrozenGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _inactivityRepository.VerifyNoOtherCalls();
        _commentRepository.VerifyNoOtherCalls();
        _countersRepository.VerifyNoOtherCalls();
        _eventProducer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WarnFrozenGames_WarnEveryGameInTheBatchOnce()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetFrozenGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });
        _commentRepository
            .Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>()))
            .ReturnsAsync(10);

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
        _inactivityRepository.Setup(r => r.GetFrozenGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { failingGameId, healthyGameId });
        _commentRepository
            .Setup(r => r.Count(failingGameId, It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>()))
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
            .Setup(r => r.GetWarnedFrozenGamesToClose(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((threshold, _) => thresholds.Add(threshold))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        thresholds.Should().Equal(TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task CloseFrozenGames_WhenNoGamesFound_TouchNothing()
    {
        _inactivityRepository
            .Setup(r => r.GetWarnedFrozenGamesToClose(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(
            r => r.GetWarnedFrozenGamesToClose(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _inactivityRepository.VerifyNoOtherCalls();
        _commentRepository.VerifyNoOtherCalls();
        _countersRepository.VerifyNoOtherCalls();
        _eventProducer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CloseFrozenGames_CloseEveryGameInTheBatchAndAnnounceIt()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository
            .Setup(r => r.GetWarnedFrozenGamesToClose(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });

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
            .Setup(r => r.GetWarnedFrozenGamesToClose(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { failingGameId, healthyGameId });
        _inactivityRepository.Setup(r => r.CloseGame(failingGameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        _closedGames.Should().Equal(healthyGameId);
        _sentEvents.Should().Equal((EventType.StatusGameClosed, healthyGameId));
    }

    #endregion
}
