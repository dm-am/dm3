using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Events;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Features.Comments;
using Microsoft.Extensions.Logging;

namespace DM.Domain.Game.Features.Inactivity;

/// <inheritdoc />
internal class GameInactivityProcessor : IGameInactivityProcessor
{
    private readonly IInactivityRepository _inactivityRepository;
    private readonly IGameCommentRepository _commentRepository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _eventProducer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly ILogger<GameInactivityProcessor> _logger;

    private const string InactivityWarningMessage =
        "Месяц без постов. Неделя на активацию. По прошествии срока игра будет переведена в замороженное состояние. Мастер в любой момент может снова начать эту игру.";

    private const string ClosureWarningMessage =
        "Три месяца в замороженном состоянии. Неделя на активацию. По прошествии срока игра будет переведена в закрытое состояние. Мастер в любой момент может снова начать эту игру.";

    private static readonly TimeSpan InactivityThreshold = TimeSpan.FromDays(30);

    private static readonly TimeSpan WarningGracePeriod = TimeSpan.FromDays(7);

    private static readonly TimeSpan FrozenThreshold = TimeSpan.FromDays(90);

    public GameInactivityProcessor(
        IInactivityRepository inactivityRepository,
        IGameCommentRepository commentRepository,
        IUnreadCountersRepository countersRepository,
        IEventProducer eventProducer,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        ILogger<GameInactivityProcessor> logger)
    {
        _inactivityRepository = inactivityRepository;
        _commentRepository = commentRepository;
        _countersRepository = countersRepository;
        _eventProducer = eventProducer;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WarnInactiveGamesAsync(CancellationToken ct)
    {
        // One reading of the clock: the cutoff and the stamp written on the game are the same moment.
        var now = _dateTimeProvider.Now;
        await ProcessAsync(
            await _inactivityRepository.GetInactiveGamesToWarn(InactivityThreshold, now, ct),
            async gameId =>
            {
                // Comment first, stamp second: a failed stamp repeats the warning, while a failed comment
                // after the stamp would freeze the game in silence.
                await CreateSystemComment(gameId, InactivityWarningMessage, now);
                return await _inactivityRepository.SetInactivityWarning(gameId, InactivityThreshold, now, ct);
            },
            gameId => _eventProducer.SendAsync(EventType.GameInactivityWarning, gameId));
    }

    /// <inheritdoc />
    public async Task FreezeWarnedGamesAsync(CancellationToken ct)
    {
        var now = _dateTimeProvider.Now;
        await ProcessAsync(
            await _inactivityRepository.GetWarnedGamesToFreeze(WarningGracePeriod, now, ct),
            gameId => _inactivityRepository.FreezeGame(gameId, now, ct),
            gameId => _eventProducer.SendAsync(EventType.StatusGameFrozen, gameId));
    }

    /// <inheritdoc />
    public async Task WarnFrozenGamesAsync(CancellationToken ct)
    {
        var now = _dateTimeProvider.Now;
        await ProcessAsync(
            await _inactivityRepository.GetFrozenGamesToWarn(FrozenThreshold, now, ct),
            async gameId =>
            {
                // Comment before stamp, as above.
                await CreateSystemComment(gameId, ClosureWarningMessage, now);
                return await _inactivityRepository.SetClosureWarning(gameId, now, ct);
            },
            gameId => _eventProducer.SendAsync(EventType.GameClosureWarning, gameId));
    }

    /// <inheritdoc />
    public async Task CloseFrozenGamesAsync(CancellationToken ct)
    {
        var now = _dateTimeProvider.Now;
        await ProcessAsync(
            await _inactivityRepository.GetWarnedFrozenGamesToClose(WarningGracePeriod, now, ct),
            gameId => _inactivityRepository.CloseGame(gameId, ct),
            gameId => _eventProducer.SendAsync(EventType.StatusGameClosed, gameId));
    }

    // One game failing does not stop the pass, and a write that did not land is not announced: it
    // changed since the selection, and nothing was written.
    private async Task ProcessAsync(
        IEnumerable<Guid> gameIds, Func<Guid, Task<bool>> write, Func<Guid, Task> announce)
    {
        foreach (var gameId in gameIds)
        {
            try
            {
                if (await write(gameId))
                {
                    await announce(gameId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inactivity pass failed for game {GameId}", gameId);
            }
        }
    }

    private async Task CreateSystemComment(Guid gameId, string text, DateTimeOffset now)
    {
        var commentId = _guidFactory.Create();

        var entity = new CreateGameCommentEntity
        {
            CommentId = commentId,
            GameId = gameId,
            AuthorId = SystemUser.Id,
            Text = text,
            CreatedUtc = now
        };

        await _commentRepository.Create(entity);
        await _countersRepository.IncrementAsync(gameId, UnreadEntryType.Message);
    }
}
