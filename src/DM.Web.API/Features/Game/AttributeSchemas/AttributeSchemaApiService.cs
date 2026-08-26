using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <inheritdoc />
internal class AttributeSchemaApiService : IAttributeSchemaApiService
{
    private readonly IAttributeSchemaService _schemaService;
    private readonly AttributeSchemaMapper _mapper;

    /// <inheritdoc />
    public AttributeSchemaApiService(
        IAttributeSchemaService schemaService,
        AttributeSchemaMapper mapper)
    {
        _schemaService = schemaService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<AttributeSchema>> Get()
    {
        var schemata = await _schemaService.GetAllAsync();
        return new ListEnvelope<AttributeSchema>(schemata.Select(_mapper.ToSchema));
    }

    /// <inheritdoc />
    public async Task<Envelope<AttributeSchema>> Get(Guid schemaId)
    {
        var schema = await _schemaService.GetForUserAsync(schemaId);
        return new Envelope<AttributeSchema>(_mapper.ToSchema(schema));
    }

    /// <inheritdoc />
    public async Task<Envelope<AttributeSchema>> Create(AttributeSchema schema)
    {
        var createSchema = _mapper.ToCreateSchema(schema);
        var createdSchema = await _schemaService.CreateAsync(createSchema);
        return new Envelope<AttributeSchema>(_mapper.ToSchema(createdSchema));
    }

    /// <inheritdoc />
    public async Task<Envelope<AttributeSchema>> Update(Guid schemaId, UpdateAttributeSchemaRequest request)
    {
        var updateSchema = _mapper.ToUpdateSchema(request);
        updateSchema.SchemaId = schemaId;
        var updatedSchema = await _schemaService.UpdateAsync(updateSchema);
        return new Envelope<AttributeSchema>(_mapper.ToSchema(updatedSchema));
    }

    /// <inheritdoc />
    public Task Delete(Guid schemaId) => _schemaService.DeleteAsync(schemaId);
}
