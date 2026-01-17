using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.DataAccess;
using DM.Services.DataAccess.MongoIntegration;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Shared;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using DbSchema = DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes.AttributeSchema;

namespace DM.Services.Gaming.BusinessProcesses.Characters.Shared;

/// <inheritdoc />
internal class CharacterValidationRepository : MongoCollectionRepository<DbSchema>, ICharacterValidationRepository
{
    private readonly IMapper _mapper;
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public CharacterValidationRepository(
        IMapper mapper,
        DmDbContext dbContext,
        DmMongoClient client) : base(client)
    {
        _mapper = mapper;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<bool> GameRequiresAttributes(CreateCharacter createCharacter, CancellationToken cancellationToken) =>
        _dbContext.Games
            .Where(g => g.GameId == createCharacter.GameId)
            .Select(g => g.AttributeSchemaId.HasValue)
            .FirstAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<AttributeSchema> GetGameSchema(Guid gameId)
    {
        var schemaId = await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .Select(g => g.AttributeSchemaId)
            .FirstAsync();
        var schema = await Collection
            .Find(Filter.Eq(s => s.Id, schemaId.Value))
            .FirstAsync();
        return _mapper.Map<AttributeSchema>(schema);
    }

    /// <inheritdoc />
    public async Task<AttributeSchema> GetCharacterSchema(Guid characterId)
    {
        var gameId = await _dbContext.Characters
            .Where(c => c.CharacterId == characterId)
            .Select(c => c.GameId)
            .FirstAsync();
        return await GetGameSchema(gameId);
    }
}