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

        // Filtering the catalog settles duplicates, which would be two rows in the link table.
        var tags = await _gameRepository.GetTags();
        var resolved = tags.Where(t => requested.Contains(t.ShortId)).ToList();

        // Only a submitted set is measured: a game imported from DM2 over a group limit stays savable
        // until its master touches the tags. The DM2 importer writes the link rows, not this path.
        TagGroupLimit.ThrowIfExceeded(resolved);

        return resolved.Select(t => t.Id).ToList();
    }
}
