using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Gaming.Dto.Output;
using DM.Services.Gaming.Dto.Shared;

namespace DM.Services.Gaming.BusinessProcesses.Games.Creating.Facades;

/// <summary>
/// Facade for resolving game creation data from external sources
/// </summary>
public interface IGameCreationDataResolver
{
    /// <summary>
    /// Get all available tag IDs
    /// </summary>
    Task<IEnumerable<Guid>> GetAvailableTagIds();

    /// <summary>
    /// Find assistant user ID by login
    /// </summary>
    /// <returns>Tuple of (exists, userId)</returns>
    Task<(bool exists, Guid userId)> FindAssistantId(string login);

    /// <summary>
    /// Get attribute schema if user is allowed to use it
    /// </summary>
    /// <returns>Schema ID if allowed, null otherwise</returns>
    Task<Guid?> GetAllowedSchemaId(Guid schemaId);
}
