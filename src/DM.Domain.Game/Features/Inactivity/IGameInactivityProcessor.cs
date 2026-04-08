using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Inactivity;

/// <summary>
/// Processes inactive games according to inactivity policy:
/// - Active games with no posts for 1 month get warned
/// - Warned games with no posts for 1 week get frozen
/// - Frozen games after 3 months get warned about closure
/// - Warned frozen games after 1 week get closed
/// </summary>
public interface IGameInactivityProcessor
{
    /// <summary>
    /// Warn active games with no posts for 1 month
    /// Creates a comment from system user and sends notification
    /// </summary>
    Task WarnInactiveGamesAsync(CancellationToken ct);

    /// <summary>
    /// Freeze games that were warned 1 week ago and still have no new posts
    /// Sets Status=Closed, ClosedReason=Frozen
    /// </summary>
    Task FreezeWarnedGamesAsync(CancellationToken ct);

    /// <summary>
    /// Warn frozen games that have been frozen for 3 months
    /// Creates a comment from system user and sends notification
    /// </summary>
    Task WarnFrozenGamesAsync(CancellationToken ct);

    /// <summary>
    /// Close frozen games that were warned 1 week ago
    /// Sets ClosedReason=None (simply closed)
    /// </summary>
    Task CloseFrozenGamesAsync(CancellationToken ct);
}
