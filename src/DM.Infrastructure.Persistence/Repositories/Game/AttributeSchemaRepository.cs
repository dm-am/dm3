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
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;

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
            .Find(Filter.Eq(s => s.Type, SchemaType.Public) | Filter.Eq(s => s.UserId, userId))
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
            .Find(Filter.Eq(s => s.Id, schemaId))
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
                Constraints = new DbStringConstraints { Required = false, MaxLength = 0 }
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
                Id = s.Id ?? _guidFactory.Create(),
                Title = s.Title.Trim(),
                Constraints = new DbStringConstraints { Required = false, MaxLength = 0 }
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

    public async Task Delete(Guid schemaId) =>
        await Collection.DeleteOneAsync(Filter.Eq(s => s.Id, schemaId));

    // --- HELPERS ---

    private async Task<IEnumerable<GeneralUser>> GetSchemataAuthors(ICollection<Guid> userIds)
    {
        return await _dbContext.Users
            .TagWith("DM.AttributeSchema.GetAuthors")
            .Where(u => userIds.Contains(u.UserId))
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }
}
