using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Inactivity;

/// <summary>
/// Repository for game inactivity management
/// </summary>
/// <remarks>
/// <para>
/// The moment a pass runs at is passed in rather than read here. The cutoff that
/// selects games and the timestamp the processor writes on them have to be the
/// same instant, and a test that moves that clock has to move both.
/// </para>
/// <para>
/// Every write of a pass carries the state its selection was made on and answers
/// whether it landed. A pass reads its whole batch once and then works through it
/// game by game, so a master who posts or restarts a game in between would
/// otherwise be answered by a write decided minutes earlier - the silence was
/// checked in the SELECT and nowhere else. Carrying the state into the WHERE
/// clause moves that check to the moment of the write, and the false answer is
/// what tells the processor not to announce something it did not do. A second
/// instance running the same pass is the same story with a shorter window.
/// </para>
/// </remarks>
public interface IInactivityRepository
{
    /// <summary>
    /// Get active games with no posts for specified duration that haven't been warned yet
    /// </summary>
    /// <param name="inactivityThreshold">Minimum time since last post (or activation if no posts)</param>
    /// <param name="now">Moment the pass runs at; the cutoff is measured back from it</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of game IDs to warn</returns>
    Task<IEnumerable<Guid>> GetInactiveGamesToWarn(
        TimeSpan inactivityThreshold, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Get active games that were warned about inactivity and still have no new posts
    /// </summary>
    /// <param name="warningGracePeriod">Time since warning was sent</param>
    /// <param name="now">Moment the pass runs at; the cutoff is measured back from it</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of game IDs to freeze</returns>
    Task<IEnumerable<Guid>> GetWarnedGamesToFreeze(
        TimeSpan warningGracePeriod, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Get frozen games that have been frozen for specified duration without closure warning
    /// </summary>
    /// <param name="frozenThreshold">Minimum time since game was frozen</param>
    /// <param name="now">Moment the pass runs at; the cutoff is measured back from it</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of game IDs to warn about closure</returns>
    Task<IEnumerable<Guid>> GetFrozenGamesToWarn(
        TimeSpan frozenThreshold, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Get frozen games that were warned about closure and still haven't been restarted
    /// </summary>
    /// <param name="warningGracePeriod">Time since closure warning was sent</param>
    /// <param name="now">Moment the pass runs at; the cutoff is measured back from it</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of game IDs to close</returns>
    Task<IEnumerable<Guid>> GetWarnedFrozenGamesToClose(
        TimeSpan warningGracePeriod, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Set inactivity warning timestamp for a game that is still active, still
    /// unwarned and still silent
    /// </summary>
    /// <remarks>
    /// The cutoff is measured back from <paramref name="now" /> by
    /// <paramref name="inactivityThreshold" />, the same way the selection measures
    /// it, and that moment is what gets stamped on the game.
    /// </remarks>
    /// <param name="gameId">Game identifier</param>
    /// <param name="inactivityThreshold">Minimum time since last post (or activation if no posts)</param>
    /// <param name="now">Moment the pass runs at; the cutoff is measured back from it</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>False when the game had spoken or been warned already and nothing was written</returns>
    Task<bool> SetInactivityWarning(
        Guid gameId, TimeSpan inactivityThreshold, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Freeze a game (set Status=Closed, ClosedReason=Frozen) that is still active,
    /// still warned, and still silent since the warning
    /// </summary>
    /// <returns>False when the game had moved on and nothing was written</returns>
    Task<bool> FreezeGame(Guid gameId, DateTimeOffset closedUtc, CancellationToken ct = default);

    /// <summary>
    /// Set closure warning timestamp for a game that is still frozen and unwarned
    /// </summary>
    /// <returns>False when the game was no longer an unwarned frozen one</returns>
    Task<bool> SetClosureWarning(Guid gameId, DateTimeOffset warningUtc, CancellationToken ct = default);

    /// <summary>
    /// Close a frozen game (set ClosedReason=None) that is still frozen and warned
    /// </summary>
    /// <returns>False when the game was no longer a warned frozen one</returns>
    Task<bool> CloseGame(Guid gameId, CancellationToken ct = default);
}
