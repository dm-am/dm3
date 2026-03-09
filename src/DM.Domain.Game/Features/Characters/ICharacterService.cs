using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// Unified service for character CRUD operations
/// </summary>
public interface ICharacterService
{
    #region Create

    /// <summary>
    /// Create new character
    /// </summary>
    /// <param name="createCharacter">Create character DTO model</param>
    Task<Character> CreateAsync(CreateCharacter createCharacter);

    #endregion

    #region Read

    /// <summary>
    /// Get all game characters
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task<IEnumerable<Character>> GetAllAsync(Guid gameId);

    /// <summary>
    /// Get single character by identifier
    /// </summary>
    /// <param name="characterId">Character identifier</param>
    Task<Character> GetAsync(Guid characterId);

    /// <summary>
    /// Mark all characters in game as read
    /// </summary>
    Task MarkAsReadAsync(Guid gameId);

    #endregion

    #region Update

    /// <summary>
    /// Update existing character
    /// </summary>
    /// <param name="updateCharacter">Update character model</param>
    Task<Character> UpdateAsync(UpdateCharacter updateCharacter);

    #endregion

    #region Delete

    /// <summary>
    /// Delete existing character (soft delete)
    /// </summary>
    /// <param name="characterId">Character identifier</param>
    Task DeleteAsync(Guid characterId);

    #endregion
}
