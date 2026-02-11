using System;
using System.Threading.Tasks;
using RoomAccess = DM.Services.DataAccess.BusinessObjects.Games.Links.RoomAccess;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Creating;

/// <summary>
/// Storage for room accesses creating
/// </summary>
internal interface IRoomAccessCreatingRepository
{
    /// <summary>
    /// Save room access
    /// </summary>
    /// <param name="access">DAL model</param>
    /// <returns></returns>
    Task<Dto.Output.RoomAccess> Create(RoomAccess access);

    /// <summary>
    /// Find reader in game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="readerLogin">Reader login</param>
    /// <returns></returns>
    Task<Guid?> FindReaderId(Guid gameId, string readerLogin);

    /// <summary>
    /// Get game identifier for character
    /// </summary>
    /// <param name="characterId">Character identifier</param>
    /// <returns></returns>
    Task<Guid?> FindCharacterGameId(Guid characterId);
}