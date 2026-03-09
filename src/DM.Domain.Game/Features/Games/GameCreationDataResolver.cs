using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Users;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Features.Games;

/// <inheritdoc />
internal class GameCreationDataResolver : IGameCreationDataResolver
{
    private readonly IGameRepository _gameRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IAttributeSchemaRepository _schemaRepository;
    private readonly IIntentionManager _intentionManager;

    public GameCreationDataResolver(
        IGameRepository gameRepository,
        IUserLookupService userLookupService,
        IAttributeSchemaRepository schemaRepository,
        IIntentionManager intentionManager)
    {
        _gameRepository = gameRepository;
        _userLookupService = userLookupService;
        _schemaRepository = schemaRepository;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetAvailableTagIds()
    {
        var tags = await _gameRepository.GetTags();
        return tags.Select(t => t.Id);
    }

    /// <inheritdoc />
    public Task<(bool exists, Guid userId)> FindAssistantId(string username)
    {
        return _userLookupService.FindUserId(username);
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
