using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Inactivity;

/// <remarks>
/// The moment of the pass is passed in, so the cutoff and the stamp are one instant. Every write
/// carries the state its selection was made on in its WHERE and answers whether it landed, so a post
/// in between is not answered by a write decided minutes earlier.
/// </remarks>
public interface IInactivityRepository
{
    /// <summary>
    /// <paramref name="inactivityThreshold"/> counts from the last post, or from the activation without posts
    /// </summary>
    Task<IEnumerable<Guid>> GetInactiveGamesToWarn(
        TimeSpan inactivityThreshold, DateTimeOffset now, CancellationToken ct = default);

    Task<IEnumerable<Guid>> GetWarnedGamesToFreeze(
        TimeSpan warningGracePeriod, DateTimeOffset now, CancellationToken ct = default);

    Task<IEnumerable<Guid>> GetFrozenGamesToWarn(
        TimeSpan frozenThreshold, DateTimeOffset now, CancellationToken ct = default);

    Task<IEnumerable<Guid>> GetWarnedFrozenGamesToClose(
        TimeSpan warningGracePeriod, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// For a game still active, unwarned and silent; <paramref name="now" /> is stamped
    /// </summary>
    /// <returns>False when the game had spoken or been warned already and nothing was written</returns>
    Task<bool> SetInactivityWarning(
        Guid gameId, TimeSpan inactivityThreshold, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// For a game still active, warned and silent since the warning
    /// </summary>
    /// <returns>False when the game had moved on and nothing was written</returns>
    Task<bool> FreezeGame(Guid gameId, DateTimeOffset closedUtc, CancellationToken ct = default);

    /// <returns>False when the game was no longer an unwarned frozen one</returns>
    Task<bool> SetClosureWarning(Guid gameId, DateTimeOffset warningUtc, CancellationToken ct = default);

    /// <returns>False when the game was no longer a warned frozen one</returns>
    Task<bool> CloseGame(Guid gameId, CancellationToken ct = default);
}
