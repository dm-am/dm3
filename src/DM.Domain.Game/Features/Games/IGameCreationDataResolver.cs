using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace DM.Domain.Game.Features.Games;
/// <summary>
/// Facade for resolving game creation data from external sources
/// </summary>
public interface IGameCreationDataResolver
{
    /// <summary>
    /// Get all available tag IDs
    /// </summary>
    Task<IEnumerable<Guid>> GetAvailableTagIds();
    /// Find assistant user ID by username
    /// <returns>Tuple of (exists, userId)</returns>
    Task<(bool exists, Guid userId)> FindAssistantId(string username);
    /// Get attribute schema if user is allowed to use it
    /// <returns>Schema ID if allowed, null otherwise</returns>
    Task<Guid?> GetAllowedSchemaId(Guid schemaId);
}
