using System;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.AttributeSchemas.Reading;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Deleting;

/// <inheritdoc />
internal class AttributeSchemaDeletingService : IAttributeSchemaDeletingService
{
    private readonly IAttributeSchemaReadingService _readingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IAttributeSchemaDeletingRepository _repository;

    /// <inheritdoc />
    public AttributeSchemaDeletingService(
        IAttributeSchemaReadingService readingService,
        IIntentionManager intentionManager,
        IAttributeSchemaDeletingRepository repository)
    {
        _readingService = readingService;
        _intentionManager = intentionManager;
        _repository = repository;
    }
        
    /// <inheritdoc />
    public async Task Delete(Guid schemaId)
    {
        var schema = await _readingService.Get(schemaId);
        _intentionManager.ThrowIfForbidden(AttributeSchemaIntention.Delete, schema);
        await _repository.Delete(schemaId);
    }
}