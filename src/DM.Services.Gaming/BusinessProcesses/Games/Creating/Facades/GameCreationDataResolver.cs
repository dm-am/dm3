using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Games.Reading;
using DM.Services.Gaming.BusinessProcesses.Games.Shared;
using DM.Services.Gaming.BusinessProcesses.Schemas.Reading;

namespace DM.Services.Gaming.BusinessProcesses.Games.Creating.Facades;

/// <inheritdoc />
internal class GameCreationDataResolver : IGameCreationDataResolver
{
    private readonly IGameReadingService _gameReadingService;
    private readonly IUserRepository _userRepository;
    private readonly ISchemaReadingRepository _schemaRepository;
    private readonly IIntentionManager _intentionManager;

    public GameCreationDataResolver(
        IGameReadingService gameReadingService,
        IUserRepository userRepository,
        ISchemaReadingRepository schemaRepository,
        IIntentionManager intentionManager)
    {
        _gameReadingService = gameReadingService;
        _userRepository = userRepository;
        _schemaRepository = schemaRepository;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetAvailableTagIds()
    {
        var tags = await _gameReadingService.GetTags();
        return tags.Select(t => t.Id);
    }

    /// <inheritdoc />
    public Task<(bool exists, Guid userId)> FindAssistantId(string login)
    {
        return _userRepository.FindUserId(login);
    }

    /// <inheritdoc />
    public async Task<Guid?> GetAllowedSchemaId(Guid schemaId)
    {
        var schema = await _schemaRepository.GetSchema(schemaId);
        if (_intentionManager.IsAllowed(AttributeSchemaIntention.Use, schema))
        {
            return schemaId;
        }
        return null;
    }
}
