using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Characters.Updating;

/// <summary>
/// Service for character updating
/// </summary>
public interface ICharacterUpdatingService
{
    /// <summary>
    /// Update existing character
    /// </summary>
    /// <param name="updateCharacter">Update character model</param>
    /// <returns></returns>
    Task<Character> Update(UpdateCharacter updateCharacter);
}