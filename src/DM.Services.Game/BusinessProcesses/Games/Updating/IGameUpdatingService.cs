using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Games.Updating;

/// <summary>
/// Service for game update
/// </summary>
public interface IGameUpdatingService
{
    /// <summary>
    /// Update existing game
    /// </summary>
    /// <param name="updateGame">Update game model</param>
    /// <returns></returns>
    Task<GameExtended> Update(UpdateGame updateGame);
}