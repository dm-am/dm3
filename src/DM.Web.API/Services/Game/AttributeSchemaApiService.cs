using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Game.BusinessProcesses.AttributeSchemas.Creating;
using DM.Services.Game.BusinessProcesses.AttributeSchemas.Deleting;
using DM.Services.Game.BusinessProcesses.AttributeSchemas.Reading;
using DM.Services.Game.BusinessProcesses.AttributeSchemas.Updating;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games.Attributes;
using DtoAttributeSchema = DM.Services.Game.Dto.Shared.AttributeSchema;

namespace DM.Web.API.Services.Game;

/// <inheritdoc />
internal class AttributeSchemaApiService : IAttributeSchemaApiService
{
    private readonly IAttributeSchemaReadingService readingService;
    private readonly IAttributeSchemaCreatingService creatingService;
    private readonly IAttributeSchemaUpdatingService updatingService;
    private readonly IAttributeSchemaDeletingService deletingService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public AttributeSchemaApiService(
        IAttributeSchemaReadingService readingService,
        IAttributeSchemaCreatingService creatingService,
        IAttributeSchemaUpdatingService updatingService,
        IAttributeSchemaDeletingService deletingService,
        IMapper mapper)
    {
        this.readingService = readingService;
        this.creatingService = creatingService;
        this.updatingService = updatingService;
        this.deletingService = deletingService;
        this.mapper = mapper;
    }
        
    /// <inheritdoc />
    public async Task<ListEnvelope<AttributeSchema>> Get()
    {
        var schemata = await readingService.Get();
        return new ListEnvelope<AttributeSchema>(schemata.Select(mapper.Map<AttributeSchema>));
    }

    /// <inheritdoc />
    public async Task<Envelope<AttributeSchema>> Get(Guid schemaId)
    {
        var schema = await readingService.Get(schemaId);
        return new Envelope<AttributeSchema>(mapper.Map<AttributeSchema>(schema));
    }

    /// <inheritdoc />
    public async Task<Envelope<AttributeSchema>> Create(AttributeSchema schema)
    {
        var createSchema = mapper.Map<DtoAttributeSchema>(schema);
        var createdSchema = await creatingService.Create(createSchema);
        return new Envelope<AttributeSchema>(mapper.Map<AttributeSchema>(createdSchema));
    }

    /// <inheritdoc />
    public async Task<Envelope<AttributeSchema>> Update(Guid schemaId, AttributeSchema schema)
    {
        var updateSchema = mapper.Map<DtoAttributeSchema>(schema);
        updateSchema.Id = schemaId;
        var updatedSchema = await updatingService.Update(updateSchema);
        return new Envelope<AttributeSchema>(mapper.Map<AttributeSchema>(updatedSchema));
    }

    /// <inheritdoc />
    public Task Delete(Guid schemaId) => deletingService.Delete(schemaId);
}