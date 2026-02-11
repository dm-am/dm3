using System;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Schemas.Reading;

namespace DM.Services.Game.BusinessProcesses.Schemas.Deleting;

/// <inheritdoc />
internal class SchemaDeletingService : ISchemaDeletingService
{
    private readonly ISchemaReadingService readingService;
    private readonly IIntentionManager intentionManager;
    private readonly ISchemaDeletingRepository repository;

    /// <inheritdoc />
    public SchemaDeletingService(
        ISchemaReadingService readingService,
        IIntentionManager intentionManager,
        ISchemaDeletingRepository repository)
    {
        this.readingService = readingService;
        this.intentionManager = intentionManager;
        this.repository = repository;
    }
        
    /// <inheritdoc />
    public async Task Delete(Guid schemaId)
    {
        var schema = await readingService.Get(schemaId);
        intentionManager.ThrowIfForbidden(AttributeSchemaIntention.Delete, schema);
        await repository.Delete(schemaId);
    }
}