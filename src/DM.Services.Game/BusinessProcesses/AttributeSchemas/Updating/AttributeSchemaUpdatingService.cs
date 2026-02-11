using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.AttributeSchemas.Creating;
using DM.Services.Game.BusinessProcesses.AttributeSchemas.Reading;
using DM.Services.Game.Dto.Shared;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Updating;

/// <inheritdoc />
internal class AttributeSchemaUpdatingService : IAttributeSchemaUpdatingService
{
    private readonly IAttributeSchemaCreatingValidator _validator;
    private readonly IAttributeSchemaReadingService _readingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IAttributeSchemaFactory _schemaFactory;
    private readonly IAttributeSchemaUpdatingRepository _repository;
    private readonly IMapper _mapper;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public AttributeSchemaUpdatingService(
        IAttributeSchemaCreatingValidator validator,
        IAttributeSchemaReadingService readingService,
        IIntentionManager intentionManager,
        IAttributeSchemaFactory schemaFactory,
        IAttributeSchemaUpdatingRepository repository,
        IMapper mapper,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _readingService = readingService;
        _intentionManager = intentionManager;
        _schemaFactory = schemaFactory;
        _repository = repository;
        _mapper = mapper;
        _identityProvider = identityProvider;
    }
        
    /// <inheritdoc />
    public async Task<AttributeSchema> Update(AttributeSchema attributeSchema)
    {
        _validator.ValidateAndThrow(attributeSchema);
        var oldSchema = await _readingService.Get(attributeSchema.Id);
        _intentionManager.ThrowIfForbidden(AttributeSchemaIntention.Edit, oldSchema);

        var schemaToUpdate = _schemaFactory.CreateToUpdate(attributeSchema, _identityProvider.Current.User.UserId);
        var updatedSchema = await _repository.UpdateSchema(schemaToUpdate);
        return _mapper.Map<AttributeSchema>(updatedSchema);
    }
}