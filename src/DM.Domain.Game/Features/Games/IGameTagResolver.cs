using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Games;

public interface IGameTagResolver
{
    /// <summary>
    /// Short ids to the tags' own identifiers: an unknown id is dropped, a group over its limit is a 400
    /// </summary>
    Task<IReadOnlyCollection<Guid>> ResolveTagIds(IEnumerable<int>? shortIds);
}
