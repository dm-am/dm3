using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Game.Authorization;
using DM.Services.Game.Dto.Shared;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Creating;

/// <inheritdoc />
internal class AttributeSchemaCreatingService : IAttributeSchemaCreatingService
{
    private readonly IAttributeSchemaCreatingValidator _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IAttributeSchemaFactory _factory;
    private readonly IAttributeSchemaCreatingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public AttributeSchemaCreatingService(
        IAttributeSchemaCreatingValidator validator,
        IIntentionManager intentionManager,
        IAttributeSchemaFactory factory,
        IAttributeSchemaCreatingRepository repository,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _factory = factory;
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<AttributeSchema> Create(AttributeSchema attributeSchema)
    {
        _validator.ValidateAndThrow(attributeSchema);
        _intentionManager.ThrowIfForbidden(GameIntention.Create);

        var schemaToCreate = _factory.CreateNew(attributeSchema, _identityProvider.Current.User.UserId);
        return await _repository.Create(schemaToCreate);
    }
}