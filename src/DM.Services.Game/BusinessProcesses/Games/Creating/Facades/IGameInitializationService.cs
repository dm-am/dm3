using System;
using System.Threading.Tasks;

namespace DM.Services.Game.BusinessProcesses.Games.Creating.Facades;

/// <summary>
/// Facade for post-creation game initialization
/// </summary>
public interface IGameInitializationService
{
    /// <summary>
    /// Initialize unread counters for game and room
    /// </summary>
    Task InitializeCounters(Guid gameId, Guid roomId);

    /// <summary>
    /// Publish game created event
    /// </summary>
    Task PublishGameCreated(Guid gameId);
}
