using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DtoGameAttributeSchema = DM.Domain.Game.Features.Games.GameAttributeSchema;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IGameAttributeSchemaRepository" />
internal class GameAttributeSchemaRepository : IGameAttributeSchemaRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GameAttributeSchemaRepository(
        DmDbContext dbContext,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<DtoGameAttributeSchema> Get(Guid gameId, CancellationToken ct = default)
    {
        var stored = await _dbContext.GameAttributeSchemas
            .AsNoTracking()
            .TagWith("DM.GameAttributeSchema.Get")
            .Where(s => s.GameId == gameId)
            .FirstAsync(ct);

        return ToSchema(stored.Specifications, stored.Version);
    }

    /// <inheritdoc />
    public async Task<DtoGameAttributeSchema?> Replace(
        ReplaceGameAttributeSchema replace, CancellationToken ct = default)
    {
        DtoGameAttributeSchema? result = null;

        await RetryableWrite.Run(_dbContext, async cancellation =>
        {
            result = null;

            // The game row is not taken here, and the module order that puts it
            // first is about transactions that hold it together with another one.
            // This block holds the sheet and the values it drops, and neither is
            // decided by a read the game row would have to protect: which
            // identifiers survive is settled by the version predicate of the
            // write below, and a character added while this runs keeps its values
            // until the next replacement, which is the outcome the write path is
            // shaped around. Taking the game would only make every replacement
            // wait behind every acceptance into the roster.
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellation);

            // Read first, because which identifiers survive is a fact about the
            // stored set: a specification keeps its own only when the sheet the
            // caller read still holds it. The conditional write below is what
            // makes that read safe to act on - a sheet replaced in between fails
            // the predicate and this whole block writes nothing.
            //
            // A game holding no sheet row fails here as loudly as a read of it
            // does, and does not travel back as a conflict: every writer of a
            // game writes its sheet in the same save, so the absence is a defect
            // of that write, and answering it with "somebody replaced the sheet"
            // would send the caller to reload a row that is not there.
            var stored = await _dbContext.GameAttributeSchemas
                .AsNoTracking()
                .TagWith("DM.GameAttributeSchema.Replace.Load")
                .Where(s => s.GameId == replace.GameId)
                .Select(s => s.Specifications)
                .FirstAsync(cancellation);

            var seenVersion = replace.Version;
            var known = stored.Select(s => s.Id).ToHashSet();
            var document = AttributeSpecificationDocument.ToDocument(
                replace.Specifications,
                specification => specification.Id.HasValue && known.Contains(specification.Id.Value)
                    ? specification.Id.Value
                    : _guidFactory.Create());

            var updatedUtc = _dateTimeProvider.Now;

            var written = await _dbContext.GameAttributeSchemas
                .Where(s => s.GameId == replace.GameId && s.Version == seenVersion)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Specifications, document)
                    .SetProperty(x => x.Version, x => x.Version + 1)
                    .SetProperty(x => x.UpdatedUtc, updatedUtc), cancellation);

            if (written == 0)
            {
                // The stored version is not the one the caller read, and this is
                // the only way this method comes back empty-handed: the sheet is
                // there, somebody else moved it, and the caller has to look again.
                return;
            }

            // The row answered to the version the caller read, so what it carries
            // now is that version moved by one - and a caller who read none
            // matches no row, which is why this side of the branch has one.
            var version = seenVersion!.Value + 1;

            // Everything the new set no longer names, and not the difference of
            // the two sets: a value written against an identifier that was never
            // in the sheet is as unreadable as one whose specification was just
            // dropped. Soft-deleted characters included - their rows are as
            // unreachable as anyone else's, and leaving them would keep exactly
            // the debt this delete exists to pay.
            var characterIds = _dbContext.Characters
                .IgnoreQueryFilters()
                .Where(c => c.GameId == replace.GameId)
                .Select(c => c.CharacterId);

            var keptIds = document.Select(s => s.Id).ToArray();

            await _dbContext.CharacterAttributes
                .IgnoreQueryFilters()
                .Where(a => characterIds.Contains(a.CharacterId) && !keptIds.Contains(a.AttributeId))
                .ExecuteDeleteAsync(cancellation);

            await transaction.CommitAsync(cancellation);
            result = ToSchema(document, version);
        }, ct);

        return result;
    }

    private static DtoGameAttributeSchema ToSchema(
        IEnumerable<Entities.Game.Characters.Attributes.AttributeSpecification> specifications,
        int version) => new()
        {
            Specifications = AttributeSpecificationDocument.ToSpecifications(specifications),
            Version = version
        };
}
