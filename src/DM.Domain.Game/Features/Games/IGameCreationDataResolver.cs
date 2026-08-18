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
    /// Translate the short tag ids the caller speaks into the tags' own
    /// identifiers, dropping the ones the catalog no longer has
    /// </summary>
    Task<IReadOnlyCollection<Guid>> ResolveTagIds(IEnumerable<int>? shortIds);
    /// Get attribute schema if user is allowed to use it
    /// <returns>Schema ID if allowed, null otherwise</returns>
    Task<Guid?> GetAllowedSchemaId(Guid schemaId);
}
