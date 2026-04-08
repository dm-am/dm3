using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Characters;

/// <summary>
/// API service for game character resources
/// </summary>
public interface ICharacterApiService
{
    /// <summary>
    /// Get list of game characters
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>List envelope containing game characters</returns>
    Task<ListEnvelope<Character>> GetAll(Guid gameId);

    /// <summary>
    /// Get single character details
    /// </summary>
    /// <param name="characterId">Character identifier</param>
    /// <returns>Envelope containing character details</returns>
    Task<Envelope<CharacterDetails>> Get(Guid characterId);

    /// <summary>
    /// Create new character
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="character">Character API model</param>
    /// <returns>Envelope containing the created character</returns>
    Task<Envelope<CharacterDetails>> Create(Guid gameId, CharacterDetails character);

    /// <summary>
    /// Update existing character
    /// </summary>
    /// <param name="characterId">Character identifier</param>
    /// <param name="character">Character API model</param>
    /// <returns>Envelope containing the updated character</returns>
    Task<Envelope<CharacterDetails>> Update(Guid characterId, CharacterDetails character);

    /// <summary>
    /// Delete existing character
    /// </summary>
    /// <param name="characterId">Character identifier</param>
    Task Delete(Guid characterId);

    /// <summary>
    /// Mark all characters as read
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task MarkAsRead(Guid gameId);
}
