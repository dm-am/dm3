using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// Repository for character operations
/// </summary>
public interface ICharacterRepository
{
    #region Validation

    /// <summary>
    /// Check if game requires attributes
    /// </summary>
    Task<bool> GameRequiresAttributes(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get game's attribute schema
    /// </summary>
    Task<AttributeSchema> GetGameSchema(Guid gameId);

    /// <summary>
    /// Get character's attribute schema
    /// </summary>
    Task<AttributeSchema> GetCharacterSchema(Guid characterId);

    #endregion

    #region Read

    /// <summary>
    /// Get all characters in a game
    /// </summary>
    Task<IEnumerable<Character>> GetCharacters(Guid gameId);

    /// <summary>
    /// Find single character
    /// </summary>
    Task<Character?> FindCharacter(Guid characterId);

    /// <summary>
    /// Get character for update
    /// </summary>
    Task<CharacterToUpdate?> GetForUpdate(Guid characterId);

    /// <summary>
    /// Get character attribute IDs
    /// </summary>
    Task<IDictionary<Guid, Guid>> GetAttributeIds(Guid characterId);

    /// <summary>
    /// Check if user has other active characters in game
    /// </summary>
    Task<bool> HasOtherActiveCharacters(Guid gameId, Guid userId, Guid excludeCharacterId);

    #endregion

    #region Write

    /// <summary>
    /// Create character
    /// </summary>
    Task<Character> Create(CreateCharacterEntity createCharacter);

    /// <summary>
    /// Update character
    /// </summary>
    Task<Character> Update(UpdateCharacterEntity updateCharacter);

    /// <summary>
    /// Delete character
    /// </summary>
    /// <param name="characterId">Character identifier</param>
    /// <param name="deletedByUserId">User who removed the character</param>
    Task Delete(Guid characterId, Guid deletedByUserId);

    /// <summary>
    /// Decline pending characters when user is blocked
    /// </summary>
    Task<int> DeclinePendingCharacters(Guid gameId, Guid userId, CancellationToken ct = default);

    #endregion
}
