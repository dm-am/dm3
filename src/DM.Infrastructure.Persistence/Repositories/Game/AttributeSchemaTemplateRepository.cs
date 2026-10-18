using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.AttributeSchemaTemplates;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DbTemplate = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchemaTemplate;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IAttributeSchemaTemplateRepository" />
/// <remarks>
/// Hidden rows are left out by the global soft-delete filter, so no read here
/// spells a predicate for them. The writes below are conditional on purpose:
/// each of them asks the database the question whose answer decides the
/// refusal, instead of reading the row, deciding, and writing into whatever the
/// row has become since.
/// </remarks>
internal class AttributeSchemaTemplateRepository : IAttributeSchemaTemplateRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AttributeSchemaTemplateRepository(
        DmDbContext dbContext,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    // --- READ ---

    /// <inheritdoc />
    public async Task<IEnumerable<AttributeSchemaTemplate>> GetTemplates(CancellationToken ct = default)
    {
        var stored = await _dbContext.AttributeSchemaTemplates
            .AsNoTracking()
            .TagWith("DM.AttributeSchemaTemplate.List")
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Title)
            .ToListAsync(ct);

        return stored.Select(ToTemplate).ToList();
    }

    /// <inheritdoc />
    public async Task<AttributeSchemaTemplate?> GetTemplate(Guid templateId, CancellationToken ct = default)
    {
        var stored = await _dbContext.AttributeSchemaTemplates
            .AsNoTracking()
            .TagWith("DM.AttributeSchemaTemplate.Get")
            .FirstOrDefaultAsync(t => t.TemplateId == templateId, ct);

        return stored == null ? null : ToTemplate(stored);
    }

    // --- WRITE ---

    /// <inheritdoc />
    public async Task<AttributeSchemaTemplate> Create(
        CreateAttributeSchemaTemplate create, Guid createdByUserId, CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;
        var stored = new DbTemplate
        {
            TemplateId = _guidFactory.Create(),
            Title = create.Title.Trim(),
            Specifications = AttributeSpecificationDocument.ToDocument(
                create.Specifications, _ => _guidFactory.Create()),
            IsDefault = false,
            CreatedByUserId = createdByUserId,
            CreatedUtc = now,
            UpdatedUtc = now,
            IsRemoved = false
        };

        _dbContext.AttributeSchemaTemplates.Add(stored);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: DuplicateKey })
        {
            // A new template carries no default flag, so the only unique rule it
            // can break is the one over the title.
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateEntityException("Duplicate attribute schema template title", ex);
        }

        return ToTemplate(stored);
    }

    /// <inheritdoc />
    public async Task<AttributeSchemaTemplate?> Replace(
        ReplaceAttributeSchemaTemplate replace, CancellationToken ct = default)
    {
        var title = replace.Title.Trim();
        var document = AttributeSpecificationDocument.ToDocument(
            replace.Specifications, _ => _guidFactory.Create());
        var updatedUtc = _dateTimeProvider.Now;

        int written;
        try
        {
            written = await _dbContext.AttributeSchemaTemplates
                .Where(t => t.TemplateId == replace.TemplateId)
                .ExecuteUpdateAsync(t => t
                    .SetProperty(x => x.Title, title)
                    .SetProperty(x => x.Specifications, document)
                    .SetProperty(x => x.UpdatedUtc, updatedUtc), ct);
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateKey)
        {
            // The default flag is untouched here, so the title is again the only
            // unique rule this write can break.
            throw new DuplicateEntityException("Duplicate attribute schema template title", ex);
        }

        return written == 0 ? null : await GetTemplate(replace.TemplateId, ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// One conditional write, and a read only to name the refusal it came back
    /// with. Reading first and deciding would leave the window in which the
    /// template becomes the default one between the two.
    /// </remarks>
    public async Task<AttributeSchemaTemplateRemoval> Delete(
        Guid templateId, CancellationToken ct = default)
    {
        var written = await _dbContext.AttributeSchemaTemplates
            .Where(t => t.TemplateId == templateId && !t.IsDefault)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.IsRemoved, true), ct);

        if (written > 0)
        {
            return AttributeSchemaTemplateRemoval.Hidden;
        }

        var isDefault = await _dbContext.AttributeSchemaTemplates
            .AsNoTracking()
            .TagWith("DM.AttributeSchemaTemplate.Delete.Explain")
            .Where(t => t.TemplateId == templateId)
            .Select(t => (bool?)t.IsDefault)
            .FirstOrDefaultAsync(ct);

        return isDefault == true
            ? AttributeSchemaTemplateRemoval.RefusedAsDefault
            : AttributeSchemaTemplateRemoval.NotFound;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The flag is taken off the template that carries it before it is put on
    /// this one, because the partial unique index refuses two live defaults and
    /// the pair has to pass through a state it allows. Both writes are one
    /// transaction: a failure between them would leave the site with no default
    /// at all.
    ///
    /// Two assignments running at once are serialised by that same index. The
    /// second one clears what it can see, which is not the row the first one has
    /// just claimed, and its own claim then collides - so it is refused rather
    /// than quietly overwriting the winner.
    /// </remarks>
    public async Task<bool> MakeDefault(Guid templateId, CancellationToken ct = default)
    {
        var assigned = false;

        await RetryableWrite.Run(_dbContext, async cancellation =>
        {
            assigned = false;

            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellation);

            await _dbContext.AttributeSchemaTemplates
                .Where(t => t.IsDefault && t.TemplateId != templateId)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.IsDefault, false), cancellation);

            int written;
            try
            {
                written = await _dbContext.AttributeSchemaTemplates
                    .Where(t => t.TemplateId == templateId)
                    .ExecuteUpdateAsync(t => t.SetProperty(x => x.IsDefault, true), cancellation);
            }
            catch (PostgresException ex) when (ex.SqlState == DuplicateKey)
            {
                throw new DuplicateEntityException("Concurrent default attribute schema template", ex);
            }

            if (written == 0)
            {
                // Hidden or absent, and the template that was the default keeps
                // the flag: the transaction rolls back with the block.
                return;
            }

            await transaction.CommitAsync(cancellation);
            assigned = true;
        }, ct);

        return assigned;
    }

    // --- HELPERS ---

    /// <summary>The unique violation PostgreSQL answers a broken index with.</summary>
    private const string DuplicateKey = "23505";

    private static AttributeSchemaTemplate ToTemplate(DbTemplate stored) => new()
    {
        Id = stored.TemplateId,
        Title = stored.Title,
        IsDefault = stored.IsDefault,
        Specifications = AttributeSpecificationDocument.ToSpecifications(stored.Specifications)
    };
}
