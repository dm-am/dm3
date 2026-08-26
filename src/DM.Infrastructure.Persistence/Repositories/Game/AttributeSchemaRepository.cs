using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Games;
using Microsoft.EntityFrameworkCore;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeConstraints;
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;
using DbNumberConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.NumberAttributeConstraints;
using DbListConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints;
using DbBbCodeConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints;
using DbListValueKind = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListValueKind;
using DbListAttributeValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DtoListValue = DM.Domain.Game.Features.Games.ListValue;
using DtoSpecificationInput = DM.Domain.Game.Features.Games.IAttributeSpecificationInput;

using DM.Infrastructure.Persistence.Shared.Users;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IAttributeSchemaRepository" />
internal class AttributeSchemaRepository : IAttributeSchemaRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public AttributeSchemaRepository(
        DmDbContext dbContext,
        IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
    }

    // --- READ ---

    // The IsRemoved predicates below are spelled out on purpose: the entity is
    // excluded from the global soft-delete filter, because GetGameSchema must
    // resolve a schema its author has already removed from the lists.

    public async Task<IEnumerable<AttributeSchema>> GetSchemata(Guid userId)
    {
        var schemata = await _dbContext.AttributeSchemata
            .TagWith("DM.AttributeSchema.List")
            .Where(s => !s.IsRemoved && (s.Type == SchemaType.Public || s.UserId == userId))
            .ToListAsync();

        if (schemata.Count == 0)
        {
            return Enumerable.Empty<AttributeSchema>();
        }

        var authorIds = schemata
            .Where(s => s.UserId.HasValue)
            .Select(s => s.UserId!.Value)
            .ToHashSet();

        var authors = (await GetSchemataAuthors(authorIds)).ToDictionary(u => u.UserId);
        var result = new List<AttributeSchema>(schemata.Count);

        foreach (var schema in schemata)
        {
            var attributeSchema = schema.ToAttributeSchema();
            attributeSchema.Author = schema.UserId.HasValue &&
                                     authors.TryGetValue(schema.UserId.Value, out var author)
                ? author
                : null;
            result.Add(attributeSchema);
        }

        return result;
    }

    public async Task<AttributeSchema?> GetSchema(Guid schemaId)
    {
        var schema = await _dbContext.AttributeSchemata
            .TagWith("DM.AttributeSchema.Get")
            .Where(s => s.AttributeSchemaId == schemaId && !s.IsRemoved)
            .FirstOrDefaultAsync();

        if (schema == null)
        {
            return null;
        }

        var result = schema.ToAttributeSchema();
        if (schema.UserId.HasValue)
        {
            result.Author = (await GetSchemataAuthors(new[] { schema.UserId.Value })).FirstOrDefault();
        }

        return result;
    }

    // Read through the game's own reference, and deliberately not through
    // GetSchema above: the removal flag hides a schema from the lists a user
    // picks one from, while a game already built on it keeps resolving it - see
    // the comment on Delete. The author is not read here either, because the
    // character screens this feeds never show it.
    public async Task<AttributeSchema> GetGameSchema(Guid gameId)
    {
        var schemaId = await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .Select(g => g.AttributeSchemaId)
            .FirstAsync();

        var schema = await _dbContext.AttributeSchemata
            .TagWith("DM.AttributeSchema.GetGameSchema")
            .Where(s => s.AttributeSchemaId == schemaId!.Value)
            .FirstAsync();

        return schema.ToAttributeSchema();
    }

    // --- WRITE ---

    public async Task<AttributeSchema> Create(CreateAttributeSchema createSchema, Guid authorId)
    {
        var schemaId = _guidFactory.Create();
        var dbSchema = new DbAttributeSchema
        {
            AttributeSchemaId = schemaId,
            Title = createSchema.Title.Trim(),
            UserId = authorId,
            Type = createSchema.Type,
            IsRemoved = false,
            Specifications = createSchema.Specifications.Select(s => new DbAttributeSpecification
            {
                Id = _guidFactory.Create(),
                Title = s.Title.Trim(),
                Order = s.Order,
                IsDescriptor = s.IsDescriptor,
                IsHidden = s.IsHidden,
                Constraints = BuildConstraints(s, s.Required)
            }).ToList()
        };

        _dbContext.AttributeSchemata.Add(dbSchema);
        await _dbContext.SaveChangesAsync();

        var result = dbSchema.ToAttributeSchema();
        var author = await _dbContext.Users
            .TagWith("DM.AttributeSchema.Create.GetAuthor")
            .Where(u => u.UserId == authorId)
            .ProjectToGeneralUser()
            .FirstOrDefaultAsync();

        result.Author = author;
        return result;
    }

    public async Task<AttributeSchema> Update(UpdateAttributeSchema updateSchema)
    {
        var existingSchema = await _dbContext.AttributeSchemata
            .TagWith("DM.AttributeSchema.Update.Load")
            .Where(s => s.AttributeSchemaId == updateSchema.SchemaId)
            .FirstAsync();

        if (updateSchema.Title != null)
        {
            existingSchema.Title = updateSchema.Title.Trim();
        }

        if (updateSchema.Type.HasValue)
        {
            existingSchema.Type = updateSchema.Type.Value;
        }

        if (updateSchema.Specifications != null)
        {
            existingSchema.Specifications = updateSchema.Specifications.Select(s => new DbAttributeSpecification
            {
                Id = s.Id.HasValue && s.Id.Value != Guid.Empty ? s.Id.Value : _guidFactory.Create(),
                Title = s.Title.Trim(),
                Order = s.Order,
                IsDescriptor = s.IsDescriptor,
                IsHidden = s.IsHidden,
                Constraints = BuildConstraints(s, s.Required)
            }).ToList();
        }

        await _dbContext.SaveChangesAsync();

        var result = existingSchema.ToAttributeSchema();
        if (existingSchema.UserId.HasValue)
        {
            result.Author = (await GetSchemataAuthors(new[] { existingSchema.UserId.Value })).FirstOrDefault();
        }

        return result;
    }

    // Soft delete: the class declares IRemovable, and a game row can still point
    // at this id through the FK, so the row has to outlive the delete for the
    // reference to keep resolving. Both reads filter the flag instead.
    public async Task Delete(Guid schemaId) =>
        await _dbContext.AttributeSchemata
            .Where(s => s.AttributeSchemaId == schemaId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRemoved, true));

    public async Task<bool> IsUsedByUserGame(Guid schemaId, Guid userId) =>
        await _dbContext.Games
            .TagWith("DM.AttributeSchema.IsUsedByUserGame")
            .AnyAsync(g => g.AttributeSchemaId == schemaId && !g.IsRemoved &&
                           (g.MasterId == userId ||
                            g.Assistants.Any(a => a.UserId == userId) ||
                            g.Characters.Any(c => c.AuthorId == userId)));

    // Deliberately not narrowed to a user: the reference that breaks belongs to
    // somebody else's game, and the schema author is exactly the person the
    // caller-narrowed query answers "no" for.
    public async Task<bool> IsUsedByAnyGame(Guid schemaId) =>
        await _dbContext.Games
            .TagWith("DM.AttributeSchema.IsUsedByAnyGame")
            .AnyAsync(g => g.AttributeSchemaId == schemaId && !g.IsRemoved);

    // --- HELPERS ---

    /// <summary>
    /// Build strongly-typed persistence constraints from a write-side specification
    /// </summary>
    /// <remarks>
    /// Internal rather than private so the round trip of an absent limit can be
    /// asserted end to end. The absence is written here and read back by the
    /// mapping profile, and a test that sees only one of the two halves stays
    /// green while the other half turns the absence back into a zero.
    /// </remarks>
    internal static DbConstraints BuildConstraints(DtoSpecificationInput spec, bool required) =>
        spec.Type switch
        {
            AttributeSpecificationType.Number =>
                new DbNumberConstraints { Required = required, MaxLength = spec.MaxLength },
            AttributeSpecificationType.TextList =>
                new DbListConstraints { Required = required, Kind = DbListValueKind.Text, Values = MapValues(spec.Values) },
            AttributeSpecificationType.NumberList =>
                new DbListConstraints { Required = required, Kind = DbListValueKind.Number, Values = MapValues(spec.Values) },
            AttributeSpecificationType.TextNumberList =>
                new DbListConstraints { Required = required, Kind = DbListValueKind.TextNumber, Values = MapValues(spec.Values) },
            AttributeSpecificationType.BbCode =>
                new DbBbCodeConstraints { Required = required, MaxLength = spec.MaxLength },
            _ =>
                new DbStringConstraints { Required = required, MaxLength = spec.MaxLength }
        };

    private static List<DbListAttributeValue> MapValues(IEnumerable<DtoListValue> values) =>
        (values ?? []).Select(v => new DbListAttributeValue { Value = v.Value, Modifier = v.Modifier }).ToList();

    private async Task<IEnumerable<GeneralUser>> GetSchemataAuthors(ICollection<Guid> userIds)
    {
        return await _dbContext.Users
            .TagWith("DM.AttributeSchema.GetAuthors")
            .Where(u => userIds.Contains(u.UserId))
            .ProjectToGeneralUser()
            .ToArrayAsync();
    }
}
