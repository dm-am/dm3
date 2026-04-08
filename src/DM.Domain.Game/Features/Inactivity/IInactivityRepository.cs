using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Inactivity;

/// <summary>
/// Repository for game inactivity management
/// </summary>
public interface IInactivityRepository
{
    /// <summary>
    /// Get active games with no posts for specified duration that haven't been warned yet
    /// </summary>
    /// <param name="inactivityThreshold">Minimum time since last post (or activation if no posts)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of game IDs to warn</returns>
    Task<IEnumerable<Guid>> GetInactiveGamesToWarn(TimeSpan inactivityThreshold, CancellationToken ct = default);

    /// <summary>
    /// Get active games that were warned about inactivity and still have no new posts
    /// </summary>
    /// <param name="warningGracePeriod">Time since warning was sent</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of game IDs to freeze</returns>
    Task<IEnumerable<Guid>> GetWarnedGamesToFreeze(TimeSpan warningGracePeriod, CancellationToken ct = default);

    /// <summary>
    /// Get frozen games that have been frozen for specified duration without closure warning
    /// </summary>
    /// <param name="frozenThreshold">Minimum time since game was frozen</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of game IDs to warn about closure</returns>
    Task<IEnumerable<Guid>> GetFrozenGamesToWarn(TimeSpan frozenThreshold, CancellationToken ct = default);

    /// <summary>
    /// Get frozen games that were warned about closure and still haven't been restarted
    /// </summary>
    /// <param name="warningGracePeriod">Time since closure warning was sent</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of game IDs to close</returns>
    Task<IEnumerable<Guid>> GetWarnedFrozenGamesToClose(TimeSpan warningGracePeriod, CancellationToken ct = default);

    /// <summary>
    /// Set inactivity warning timestamp for a game
    /// </summary>
    Task SetInactivityWarning(Guid gameId, DateTimeOffset warningUtc, CancellationToken ct = default);

    /// <summary>
    /// Freeze a game (set Status=Closed, ClosedReason=Frozen)
    /// </summary>
    Task FreezeGame(Guid gameId, DateTimeOffset closedUtc, CancellationToken ct = default);

    /// <summary>
    /// Set closure warning timestamp for a frozen game
    /// </summary>
    Task SetClosureWarning(Guid gameId, DateTimeOffset warningUtc, CancellationToken ct = default);

    /// <summary>
    /// Close a frozen game (set ClosedReason=None)
    /// </summary>
    Task CloseGame(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Get game title by ID (for notification messages)
    /// </summary>
    Task<string?> GetGameTitle(Guid gameId, CancellationToken ct = default);
}
