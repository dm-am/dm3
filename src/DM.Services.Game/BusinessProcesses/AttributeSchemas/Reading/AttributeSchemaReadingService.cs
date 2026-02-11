using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Exceptions;
using DM.Services.Game.Dto.Shared;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Reading;

/// <inheritdoc />
internal class AttributeSchemaReadingService : IAttributeSchemaReadingService
{
    private readonly IAttributeSchemaReadingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public AttributeSchemaReadingService(
        IAttributeSchemaReadingRepository repository,
        IIdentityProvider identityProvider)
    {
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AttributeSchema>> Get() =>
        await _repository.GetSchemata(_identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public async Task<AttributeSchema> Get(Guid schemaId)
    {
        var attributeSchema = await _repository.GetSchema(schemaId);
        if (attributeSchema == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Schema not found");
        }

        return attributeSchema;
    }
}