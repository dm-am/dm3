using System.Threading.Tasks;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Output;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Game.BusinessProcesses.Games.Updating;

/// <summary>
/// Game updating storage
/// </summary>
internal interface IGameUpdatingRepository
{
    /// <summary>
    /// Save game changes
    /// </summary>
    /// <param name="updateGame">Game changes</param>
    /// <returns></returns>
    Task<GameExtended> Update(IUpdateBuilder<DbGame> updateGame);
}