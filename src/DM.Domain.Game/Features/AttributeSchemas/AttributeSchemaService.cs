using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using FluentValidation;


namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// Unified service for attribute schema CRUD operations
/// </summary>
internal class AttributeSchemaService : IAttributeSchemaService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IAttributeSchemaRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IValidator<CreateAttributeSchema> _createValidator;
    private readonly IValidator<UpdateAttributeSchema> _updateValidator;

    public AttributeSchemaService(
        IIntentionManager intentionManager,
        IAttributeSchemaRepository repository,
        IIdentityProvider identityProvider,
        IValidator<CreateAttributeSchema> createValidator,
        IValidator<UpdateAttributeSchema> updateValidator)
    {
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    #region Create

    public async Task<AttributeSchema> CreateAsync(CreateAttributeSchema createSchema)
    {
        _intentionManager.ThrowIfForbidden(GameIntention.Create);
        await _createValidator.ValidateAndThrowAsync(createSchema);
        return await _repository.Create(createSchema, _identityProvider.Current.User.UserId);
    }

    #endregion

    #region Read

    public async Task<IEnumerable<AttributeSchema>> GetAllAsync() =>
        await _repository.GetSchemata(_identityProvider.Current.User.UserId);

    /// <summary>
    /// Internal ungated read used by render paths (character/game details).
    /// Never throws 403 so a plain player can view a private-schema game.
    /// </summary>
    public async Task<AttributeSchema> GetAsync(Guid schemaId)
    {
        var attributeSchema = await _repository.GetSchema(schemaId);
        if (attributeSchema == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Schema not found");
        }
        return attributeSchema;
    }

    public async Task<AttributeSchema> GetForUserAsync(Guid schemaId)
    {
        var schema = await GetAsync(schemaId);
        var userId = _identityProvider.Current.User.UserId;

        var allowed = schema.Type == SchemaType.Public
                      || schema.Author?.UserId == userId
                      || await _repository.IsUsedByUserGame(schemaId, userId);

        if (!allowed)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Not allowed to read this schema");
        }

        return schema;
    }

    #endregion

    #region Update

    public async Task<AttributeSchema> UpdateAsync(UpdateAttributeSchema updateSchema)
    {
        var oldSchema = await GetAsync(updateSchema.SchemaId);
        _intentionManager.ThrowIfForbidden(AttributeSchemaIntention.Edit, oldSchema);
        await _updateValidator.ValidateAndThrowAsync(updateSchema);
        return await _repository.Update(updateSchema);
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid schemaId)
    {
        var schema = await GetAsync(schemaId);
        _intentionManager.ThrowIfForbidden(AttributeSchemaIntention.Delete, schema);

        // Nothing below enforces this reference: the schema is a Mongo document
        // and the game row pointing at it is in Postgres. Reads of a game resolve
        // the reference and turn a schema that is gone into a 404 for the whole
        // game, so deleting a public schema closes every game built on it - other
        // people's games, whose masters can repair nothing.
        if (await _repository.IsUsedByAnyGame(schemaId))
        {
            throw new HttpException(HttpStatusCode.Conflict,
                "Schema is used by a game and cannot be deleted");
        }

        await _repository.Delete(schemaId);
    }

    #endregion
}
