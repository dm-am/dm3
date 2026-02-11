using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Characters.Creating;

/// <summary>
/// Service to create new characters
/// </summary>
public interface ICharacterCreatingService
{
    /// <summary>
    /// Create new character
    /// </summary>
    /// <param name="createCharacter">Create character DTO model</param>
    /// <returns></returns>
    Task<Character> Create(CreateCharacter createCharacter);
}