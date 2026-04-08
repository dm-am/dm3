using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Games;
using Microsoft.Extensions.Logging;

namespace DM.Domain.Game.Features.Inactivity;

/// <inheritdoc />
internal class GameInactivityProcessor : IGameInactivityProcessor
{
    /// <summary>
    /// System user ID (Robot Administrator) - well-known constant
    /// </summary>
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IInactivityRepository _inactivityRepository;
    private readonly IGameCommentRepository _commentRepository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _eventProducer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly ILogger<GameInactivityProcessor> _logger;

    /// <summary>
    /// Warning message for inactive games (1 month without posts)
    /// </summary>
    private const string InactivityWarningMessage =
        "Месяц без постов. Неделя на активацию. По прошествии срока модуль будет переведен в замороженное состояние. Мастер в любой момент может снова начать эту игру.";

    /// <summary>
    /// Warning message for frozen games (3 months frozen)
    /// </summary>
    private const string ClosureWarningMessage =
        "Три месяца в замороженном состоянии. Неделя на активацию. По прошествии срока модуль будет переведен в закрытое состояние. Мастер в любой момент может снова начать эту игру.";

    /// <summary>
    /// Inactivity threshold before warning (1 month)
    /// </summary>
    private static readonly TimeSpan InactivityThreshold = TimeSpan.FromDays(30);

    /// <summary>
    /// Grace period after warning before action (1 week)
    /// </summary>
    private static readonly TimeSpan WarningGracePeriod = TimeSpan.FromDays(7);

    /// <summary>
    /// Time frozen before closure warning (3 months)
    /// </summary>
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
        var gameIds = await _inactivityRepository.GetInactiveGamesToWarn(InactivityThreshold, ct);
        var now = _dateTimeProvider.Now;

        foreach (var gameId in gameIds)
        {
            try
            {
                await CreateSystemComment(gameId, InactivityWarningMessage, ct);
                await _inactivityRepository.SetInactivityWarning(gameId, now, ct);
                await _eventProducer.SendAsync(EventType.GameInactivityWarning, gameId);

                _logger.LogInformation("Sent inactivity warning for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to warn inactive game {GameId}", gameId);
            }
        }
    }

    /// <inheritdoc />
    public async Task FreezeWarnedGamesAsync(CancellationToken ct)
    {
        var gameIds = await _inactivityRepository.GetWarnedGamesToFreeze(WarningGracePeriod, ct);
        var now = _dateTimeProvider.Now;

        foreach (var gameId in gameIds)
        {
            try
            {
                await _inactivityRepository.FreezeGame(gameId, now, ct);
                await _eventProducer.SendAsync(EventType.StatusGameFrozen, gameId);

                _logger.LogInformation("Froze inactive game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to freeze game {GameId}", gameId);
            }
        }
    }

    /// <inheritdoc />
    public async Task WarnFrozenGamesAsync(CancellationToken ct)
    {
        var gameIds = await _inactivityRepository.GetFrozenGamesToWarn(FrozenThreshold, ct);
        var now = _dateTimeProvider.Now;

        foreach (var gameId in gameIds)
        {
            try
            {
                await CreateSystemComment(gameId, ClosureWarningMessage, ct);
                await _inactivityRepository.SetClosureWarning(gameId, now, ct);
                await _eventProducer.SendAsync(EventType.GameClosureWarning, gameId);

                _logger.LogInformation("Sent closure warning for frozen game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to warn frozen game {GameId}", gameId);
            }
        }
    }

    /// <inheritdoc />
    public async Task CloseFrozenGamesAsync(CancellationToken ct)
    {
        var gameIds = await _inactivityRepository.GetWarnedFrozenGamesToClose(WarningGracePeriod, ct);

        foreach (var gameId in gameIds)
        {
            try
            {
                await _inactivityRepository.CloseGame(gameId, ct);
                await _eventProducer.SendAsync(EventType.StatusGameClosed, gameId);

                _logger.LogInformation("Closed frozen game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close frozen game {GameId}", gameId);
            }
        }
    }

    /// <summary>
    /// Creates a system comment from Robot Administrator
    /// </summary>
    private async Task CreateSystemComment(Guid gameId, string text, CancellationToken ct)
    {
        var commentId = _guidFactory.Create();
        var now = _dateTimeProvider.Now;

        // Get current comment count - we need to increment it
        var currentCount = await _commentRepository.Count(gameId, new GameCommentsQuery());

        var entity = new CreateGameCommentEntity
        {
            CommentId = commentId,
            GameId = gameId,
            AuthorId = SystemUserId,
            Text = text,
            NewCommentCount = currentCount + 1,
            CreatedUtc = now
        };

        await _commentRepository.Create(entity);
        await _countersRepository.IncrementAsync(gameId, UnreadEntryType.Message);
    }
}
