using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Inactivity;

public class GameInactivityProcessorShould : UnitTestBase
{
    private readonly Mock<IInactivityRepository> _inactivityRepository;
    private readonly Mock<IGameCommentRepository> _commentRepository;
    private readonly Mock<IUnreadCountersRepository> _countersRepository;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly GameInactivityProcessor _processor;
    private readonly DateTimeOffset _now;

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

        _countersRepository.Setup(r => r.IncrementAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Returns(Task.CompletedTask);
        _eventProducer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()))
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
    public async Task WarnInactiveGames_WhenNoGamesFound_DoNothing()
    {
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        _commentRepository.Verify(r => r.Create(It.IsAny<CreateGameCommentEntity>()), Times.Never);
        _eventProducer.Verify(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task WarnInactiveGames_CreateCommentForEachGame()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });
        _commentRepository.Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>())).ReturnsAsync(5);
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        _commentRepository.Verify(r => r.Create(It.Is<CreateGameCommentEntity>(
            c => c.GameId == gameId1)), Times.Once);
        _commentRepository.Verify(r => r.Create(It.Is<CreateGameCommentEntity>(
            c => c.GameId == gameId2)), Times.Once);
    }

    [Fact]
    public async Task WarnInactiveGames_SetWarningTimestamp()
    {
        var gameId = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _commentRepository.Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>())).ReturnsAsync(0);
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(r => r.SetInactivityWarning(gameId, _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WarnInactiveGames_SendNotificationEvent()
    {
        var gameId = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _commentRepository.Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>())).ReturnsAsync(0);
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        _eventProducer.Verify(p => p.SendAsync(EventType.GameInactivityWarning, gameId), Times.Once);
    }

    [Fact]
    public async Task WarnInactiveGames_IncrementUnreadCounter()
    {
        var gameId = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _commentRepository.Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>())).ReturnsAsync(0);
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        _countersRepository.Verify(r => r.IncrementAsync(gameId, UnreadEntryType.Message), Times.Once);
    }

    [Fact]
    public async Task WarnInactiveGames_UseSystemUserId()
    {
        var gameId = Guid.NewGuid();
        var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _commentRepository.Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>())).ReturnsAsync(0);
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        _commentRepository.Verify(r => r.Create(It.Is<CreateGameCommentEntity>(
            c => c.AuthorId == systemUserId)), Times.Once);
    }

    [Fact]
    public async Task WarnInactiveGames_ContinueOnError()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetInactiveGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });
        _commentRepository.Setup(r => r.Count(gameId1, It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>())).ThrowsAsync(new Exception("DB error"));
        _commentRepository.Setup(r => r.Count(gameId2, It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>())).ReturnsAsync(0);
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });

        await _processor.WarnInactiveGamesAsync(CancellationToken.None);

        // Second game should still be processed despite first one failing
        _commentRepository.Verify(r => r.Create(It.Is<CreateGameCommentEntity>(
            c => c.GameId == gameId2)), Times.Once);
    }

    #endregion

    #region FreezeWarnedGamesAsync

    [Fact]
    public async Task FreezeWarnedGames_WhenNoGamesFound_DoNothing()
    {
        _inactivityRepository.Setup(r => r.GetWarnedGamesToFreeze(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.FreezeWarnedGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(r => r.FreezeGame(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventProducer.Verify(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task FreezeWarnedGames_FreezeEachGame()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetWarnedGamesToFreeze(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });

        await _processor.FreezeWarnedGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(r => r.FreezeGame(gameId1, _now, It.IsAny<CancellationToken>()), Times.Once);
        _inactivityRepository.Verify(r => r.FreezeGame(gameId2, _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FreezeWarnedGames_SendFrozenEvent()
    {
        var gameId = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetWarnedGamesToFreeze(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        await _processor.FreezeWarnedGamesAsync(CancellationToken.None);

        _eventProducer.Verify(p => p.SendAsync(EventType.StatusGameFrozen, gameId), Times.Once);
    }

    [Fact]
    public async Task FreezeWarnedGames_ContinueOnError()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetWarnedGamesToFreeze(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });
        _inactivityRepository.Setup(r => r.FreezeGame(gameId1, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        await _processor.FreezeWarnedGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(r => r.FreezeGame(gameId2, _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region WarnFrozenGamesAsync

    [Fact]
    public async Task WarnFrozenGames_WhenNoGamesFound_DoNothing()
    {
        _inactivityRepository.Setup(r => r.GetFrozenGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.WarnFrozenGamesAsync(CancellationToken.None);

        _commentRepository.Verify(r => r.Create(It.IsAny<CreateGameCommentEntity>()), Times.Never);
        _eventProducer.Verify(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task WarnFrozenGames_CreateCommentForEachGame()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetFrozenGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });
        _commentRepository.Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>())).ReturnsAsync(10);
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });

        await _processor.WarnFrozenGamesAsync(CancellationToken.None);

        _commentRepository.Verify(r => r.Create(It.Is<CreateGameCommentEntity>(
            c => c.GameId == gameId1)), Times.Once);
        _commentRepository.Verify(r => r.Create(It.Is<CreateGameCommentEntity>(
            c => c.GameId == gameId2)), Times.Once);
    }

    [Fact]
    public async Task WarnFrozenGames_SetClosureWarning()
    {
        var gameId = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetFrozenGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _commentRepository.Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>())).ReturnsAsync(0);
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });

        await _processor.WarnFrozenGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(r => r.SetClosureWarning(gameId, _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WarnFrozenGames_SendClosureWarningEvent()
    {
        var gameId = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetFrozenGamesToWarn(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _commentRepository.Setup(r => r.Count(It.IsAny<Guid>(), It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>())).ReturnsAsync(0);
        _commentRepository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });

        await _processor.WarnFrozenGamesAsync(CancellationToken.None);

        _eventProducer.Verify(p => p.SendAsync(EventType.GameClosureWarning, gameId), Times.Once);
    }

    #endregion

    #region CloseFrozenGamesAsync

    [Fact]
    public async Task CloseFrozenGames_WhenNoGamesFound_DoNothing()
    {
        _inactivityRepository.Setup(r => r.GetWarnedFrozenGamesToClose(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(r => r.CloseGame(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventProducer.Verify(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CloseFrozenGames_CloseEachGame()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetWarnedFrozenGamesToClose(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(r => r.CloseGame(gameId1, It.IsAny<CancellationToken>()), Times.Once);
        _inactivityRepository.Verify(r => r.CloseGame(gameId2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseFrozenGames_SendClosedEvent()
    {
        var gameId = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetWarnedFrozenGamesToClose(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        _eventProducer.Verify(p => p.SendAsync(EventType.StatusGameClosed, gameId), Times.Once);
    }

    [Fact]
    public async Task CloseFrozenGames_ContinueOnError()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _inactivityRepository.Setup(r => r.GetWarnedFrozenGamesToClose(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId1, gameId2 });
        _inactivityRepository.Setup(r => r.CloseGame(gameId1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        await _processor.CloseFrozenGamesAsync(CancellationToken.None);

        _inactivityRepository.Verify(r => r.CloseGame(gameId2, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
