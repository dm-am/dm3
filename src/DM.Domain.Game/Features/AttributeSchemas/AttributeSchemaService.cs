using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;


namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// Unified service for attribute schema CRUD operations
/// </summary>
internal class AttributeSchemaService : IAttributeSchemaService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IAttributeSchemaRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    public AttributeSchemaService(
        IIntentionManager intentionManager,
        IAttributeSchemaRepository repository,
        IIdentityProvider identityProvider)
    {
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
    }

    #region Create

    public async Task<AttributeSchema> CreateAsync(CreateAttributeSchema createSchema)
    {
        _intentionManager.ThrowIfForbidden(GameIntention.Create);
        return await _repository.Create(createSchema, _identityProvider.Current.User.UserId);
    }

    #endregion

    #region Read

    public async Task<IEnumerable<AttributeSchema>> GetAllAsync() =>
        await _repository.GetSchemata(_identityProvider.Current.User.UserId);

    public async Task<AttributeSchema> GetAsync(Guid schemaId)
    {
        var attributeSchema = await _repository.GetSchema(schemaId);
        if (attributeSchema == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Schema not found");
        }
        return attributeSchema;
    }

    #endregion

    #region Update

    public async Task<AttributeSchema> UpdateAsync(UpdateAttributeSchema updateSchema)
    {
        var oldSchema = await GetAsync(updateSchema.SchemaId);
        _intentionManager.ThrowIfForbidden(AttributeSchemaIntention.Edit, oldSchema);
        return await _repository.Update(updateSchema);
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid schemaId)
    {
        var schema = await GetAsync(schemaId);
        _intentionManager.ThrowIfForbidden(AttributeSchemaIntention.Delete, schema);
        await _repository.Delete(schemaId);
    }

    #endregion
}
