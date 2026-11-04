using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Inactivity;

/// <summary>
/// A silent game is warned, then frozen, then warned again and closed.
/// </summary>
public interface IGameInactivityProcessor
{
    /// <summary>
    /// A comment from the system user and a notification
    /// </summary>
    Task WarnInactiveGamesAsync(CancellationToken ct);

    /// <summary>
    /// Status Closed with ClosedReason Frozen
    /// </summary>
    Task FreezeWarnedGamesAsync(CancellationToken ct);

    /// <summary>
    /// A comment from the system user and a notification
    /// </summary>
    Task WarnFrozenGamesAsync(CancellationToken ct);

    /// <summary>
    /// ClosedReason None
    /// </summary>
    Task CloseFrozenGamesAsync(CancellationToken ct);
}
