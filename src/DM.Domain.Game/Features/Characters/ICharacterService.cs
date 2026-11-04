using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Characters;

public interface ICharacterService
{
    Task<Character> CreateAsync(CreateCharacter createCharacter);

    Task<IEnumerable<Character>> GetAllAsync(Guid gameId);

    Task<Character> GetAsync(Guid characterId);

    /// <summary>
    /// Whether the viewer may edit the character, by the check the save makes:
    /// whoever may gets its BBCode values as written, to edit them.
    /// </summary>
    Task<bool> MayEditAsync(Guid characterId);

    Task MarkAsReadAsync(Guid gameId);

    Task<Character> UpdateAsync(UpdateCharacter updateCharacter);

    /// <summary>
    /// Move a character to another place in the game
    /// </summary>
    Task<Character> ChangeStatusAsync(Guid characterId, CharacterStatusTransition transition);

    /// <summary>
    /// Soft delete
    /// </summary>
    Task DeleteAsync(Guid characterId);
}
