using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Features.Games;

/// <inheritdoc />
internal class GameCreationDataResolver : IGameCreationDataResolver
{
    private readonly IGameRepository _gameRepository;
    private readonly IAttributeSchemaRepository _schemaRepository;
    private readonly IIntentionManager _intentionManager;

    public GameCreationDataResolver(
        IGameRepository gameRepository,
        IAttributeSchemaRepository schemaRepository,
        IIntentionManager intentionManager)
    {
        _gameRepository = gameRepository;
        _schemaRepository = schemaRepository;
        _intentionManager = intentionManager;
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

    /// <inheritdoc />
    public async Task<Guid?> GetAllowedSchemaId(Guid schemaId)
    {
        var schema = await _schemaRepository.GetSchema(schemaId);

        // An id that resolves to nothing is dropped without asking the resolver:
        // Use is answered from the schema's own type and author, and there is
        // neither one to read here.
        if (schema == null)
        {
            return null;
        }

        return _intentionManager.IsAllowed(AttributeSchemaIntention.Use, schema)
            ? schemaId
            : null;
    }
}
