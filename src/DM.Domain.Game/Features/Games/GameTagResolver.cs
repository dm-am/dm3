using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Games;

/// <inheritdoc />
internal class GameTagResolver : IGameTagResolver
{
    private readonly IGameRepository _gameRepository;

    public GameTagResolver(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Guid>> ResolveTagIds(IEnumerable<int>? shortIds)
    {
        var requested = shortIds?.ToHashSet();
        if (requested is not { Count: > 0 })
        {
            return [];
        }

        // Filtering the catalog rather than the request also settles duplicates:
        // the same tag twice would otherwise become two rows in the link table.
        var tags = await _gameRepository.GetTags();
        var resolved = tags.Where(t => requested.Contains(t.ShortId)).ToList();

        // The per-group limit is checked against the set that was submitted, and
        // only then. The update endpoint takes tags as an optional field where
        // absence means "leave them alone", and an absent field never reaches
        // this method, so an untouched game is never measured against the limits.
        //
        // That is deliberate, for the data still to come from DM2: an imported
        // game may arrive carrying more tags of a group than the group now
        // allows. Trimming it would throw away what its master declared, and
        // refusing it would leave the game unsavable. It heals itself the next
        // time the master touches the tags, because that submits a set. The DM2
        // importer must not apply the limits either - it writes the link rows,
        // not this path.
        TagGroupLimit.ThrowIfExceeded(resolved);

        return resolved.Select(t => t.Id).ToList();
    }
}
