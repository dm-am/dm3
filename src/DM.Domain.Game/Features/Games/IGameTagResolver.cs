using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Translates the tag identifiers a caller speaks into the tags of the catalog
/// </summary>
public interface IGameTagResolver
{
    /// <summary>
    /// Translate the short tag ids the caller speaks into the tags' own
    /// identifiers, dropping the ones the catalog no longer has
    /// </summary>
    /// <remarks>
    /// Also where a submitted set is measured against the per-group limits, so
    /// both write paths of a game are held to one rule. An unknown id is dropped
    /// quietly, a group over its limit is refused with 400.
    /// </remarks>
    Task<IReadOnlyCollection<Guid>> ResolveTagIds(IEnumerable<int>? shortIds);
}
