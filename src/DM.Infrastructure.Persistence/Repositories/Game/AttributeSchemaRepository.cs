using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.MongoIntegration;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
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

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IAttributeSchemaRepository" />
internal class AttributeSchemaRepository :
    MongoCollectionRepository<DbAttributeSchema>,
    IAttributeSchemaRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public AttributeSchemaRepository(
        DmMongoClient client,
        DmDbContext dbContext,
        IMapper mapper,
        IGuidFactory guidFactory) : base(client)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _guidFactory = guidFactory;
    }

    // --- READ ---

    public async Task<IEnumerable<AttributeSchema>> GetSchemata(Guid userId)
    {
        var schemata = await Collection
            .Find(Filter.Eq(s => s.IsRemoved, false) &
                  (Filter.Eq(s => s.Type, SchemaType.Public) | Filter.Eq(s => s.UserId, userId)))
            .ToListAsync();

        if (!schemata.Any())
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
            var attributeSchema = _mapper.Map<AttributeSchema>(schema);
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
        var schema = await Collection
            .Find(Filter.Eq(s => s.Id, schemaId) & Filter.Eq(s => s.IsRemoved, false))
            .FirstOrDefaultAsync();

        if (schema == null)
        {
            return null;
        }

        var result = _mapper.Map<AttributeSchema>(schema);
        if (schema.UserId.HasValue)
        {
            result.Author = (await GetSchemataAuthors(new[] { schema.UserId.Value })).First();
        }

        return result;
    }

    // --- WRITE ---

    public async Task<AttributeSchema> Create(CreateAttributeSchema createSchema, Guid authorId)
    {
        var schemaId = _guidFactory.Create();
        var dbSchema = new DbAttributeSchema
        {
            Id = schemaId,
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

        await Collection.InsertOneAsync(dbSchema);

        var createdSchema = await Collection
            .Find(Filter.Eq(s => s.Id, schemaId))
            .FirstAsync();

        var result = _mapper.Map<AttributeSchema>(createdSchema);
        var author = await _dbContext.Users
            .TagWith("DM.AttributeSchema.Create.GetAuthor")
            .Where(u => u.UserId == authorId)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        result.Author = author;
        return result;
    }

    public new async Task<AttributeSchema> Update(UpdateAttributeSchema updateSchema)
    {
        var existingSchema = await Collection
            .Find(Filter.Eq(s => s.Id, updateSchema.SchemaId))
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

        await Collection.ReplaceOneAsync(Filter.Eq(s => s.Id, updateSchema.SchemaId), existingSchema);

        var updatedSchema = await Collection
            .Find(Filter.Eq(s => s.Id, updateSchema.SchemaId))
            .FirstAsync();

        var result = _mapper.Map<AttributeSchema>(updatedSchema);
        if (existingSchema.UserId.HasValue)
        {
            result.Author = (await GetSchemataAuthors(new[] { existingSchema.UserId.Value })).FirstOrDefault();
        }

        return result;
    }

    // Soft delete: the class declares IRemovable, and a Postgres game row can
    // still point at this id, so the document has to outlive the delete for the
    // dangling reference to be repairable. Both reads filter the flag instead.
    public async Task Delete(Guid schemaId) =>
        await Collection.UpdateOneAsync(
            Filter.Eq(s => s.Id, schemaId),
            Builders<DbAttributeSchema>.Update.Set(s => s.IsRemoved, true));

    public async Task<bool> IsUsedByUserGame(Guid schemaId, Guid userId) =>
        await _dbContext.Games
            .TagWith("DM.AttributeSchema.IsUsedByUserGame")
            .AnyAsync(g => g.AttributeSchemaId == schemaId && !g.IsRemoved &&
                           (g.MasterId == userId ||
                            g.Assistants.Any(a => a.UserId == userId) ||
                            g.Characters.Any(c => c.AuthorId == userId)));

    // --- HELPERS ---

    /// <summary>
    /// Build strongly-typed persistence constraints from a write-side specification
    /// </summary>
    private static DbConstraints BuildConstraints(DtoSpecificationInput spec, bool required) =>
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
                new DbStringConstraints { Required = required, MaxLength = spec.MaxLength ?? 0 }
        };

    private static List<DbListAttributeValue> MapValues(IEnumerable<DtoListValue> values) =>
        (values ?? []).Select(v => new DbListAttributeValue { Value = v.Value, Modifier = v.Modifier }).ToList();

    private async Task<IEnumerable<GeneralUser>> GetSchemataAuthors(ICollection<Guid> userIds)
    {
        return await _dbContext.Users
            .TagWith("DM.AttributeSchema.GetAuthors")
            .Where(u => userIds.Contains(u.UserId))
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }
}
