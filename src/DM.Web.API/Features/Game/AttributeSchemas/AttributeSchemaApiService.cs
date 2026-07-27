using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <inheritdoc />
internal class AttributeSchemaApiService : IAttributeSchemaApiService
{
    private readonly IAttributeSchemaService _schemaService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AttributeSchemaApiService(
        IAttributeSchemaService schemaService,
        IMapper mapper)
    {
        _schemaService = schemaService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<AttributeSchema>> Get()
    {
        var schemata = await _schemaService.GetAllAsync();
        return new ListEnvelope<AttributeSchema>(schemata.Select(_mapper.Map<AttributeSchema>));
    }

    /// <inheritdoc />
    public async Task<Envelope<AttributeSchema>> Get(Guid schemaId)
    {
        var schema = await _schemaService.GetForUserAsync(schemaId);
        return new Envelope<AttributeSchema>(_mapper.Map<AttributeSchema>(schema));
    }

    /// <inheritdoc />
    public async Task<Envelope<AttributeSchema>> Create(AttributeSchema schema)
    {
        var createSchema = _mapper.Map<CreateAttributeSchema>(schema);
        var createdSchema = await _schemaService.CreateAsync(createSchema);
        return new Envelope<AttributeSchema>(_mapper.Map<AttributeSchema>(createdSchema));
    }

    /// <inheritdoc />
    public async Task<Envelope<AttributeSchema>> Update(Guid schemaId, AttributeSchema schema)
    {
        var updateSchema = _mapper.Map<UpdateAttributeSchema>(schema);
        updateSchema.SchemaId = schemaId;
        var updatedSchema = await _schemaService.UpdateAsync(updateSchema);
        return new Envelope<AttributeSchema>(_mapper.Map<AttributeSchema>(updatedSchema));
    }

    /// <inheritdoc />
    public Task Delete(Guid schemaId) => _schemaService.DeleteAsync(schemaId);
}
