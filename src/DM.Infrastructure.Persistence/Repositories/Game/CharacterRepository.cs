using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class CharacterRepository : MongoCollectionRepository<DbSchema>, ICharacterRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public CharacterRepository(
        DmDbContext dbContext,
        IMapper mapper,
        DmMongoClient client,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider) : base(client)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    #region Validation Operations

    public Task<bool> GameRequiresAttributes(Guid gameId, CancellationToken cancellationToken) =>
        _dbContext.Games
            .Where(g => g.GameId == gameId)
            .Select(g => g.AttributeSchemaId.HasValue)
            .FirstAsync(cancellationToken);

    public async Task<AttributeSchema> GetGameSchema(Guid gameId)
    {
        var schemaId = await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .Select(g => g.AttributeSchemaId)
            .FirstAsync();

        var schema = await Collection
            .Find(Filter.Eq(s => s.Id, schemaId!.Value))
            .FirstAsync();

        return _mapper.Map<AttributeSchema>(schema);
    }

    public async Task<AttributeSchema> GetCharacterSchema(Guid characterId)
    {
        var gameId = await _dbContext.Characters
            .Where(c => c.CharacterId == characterId)
            .Select(c => c.GameId)
            .FirstAsync();

        return await GetGameSchema(gameId);
    }

    #endregion

    #region Read Operations

    public async Task<IEnumerable<Character>> GetCharacters(Guid gameId)
    {
        return await _dbContext.Characters
            .Where(c => !c.IsRemoved && c.GameId == gameId)
            .OrderByDescending(c => c.CreatedUtc)
            .ProjectTo<Character>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    public async Task<Character?> FindCharacter(Guid characterId)
    {
        return await _dbContext.Characters
            .Where(c => !c.IsRemoved && c.CharacterId == characterId)
            .ProjectTo<Character>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public Task<CharacterToUpdate> GetForUpdate(Guid characterId)
    {
        return _dbContext.Characters
            .Where(c => c.CharacterId == characterId)
            .ProjectTo<CharacterToUpdate>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    public async Task<IDictionary<Guid, Guid>> GetAttributeIds(Guid characterId)
    {
        return await _dbContext.CharacterAttributes
            .Where(a => a.CharacterId == characterId)
            .ToDictionaryAsync(a => a.AttributeId, a => a.CharacterAttributeId);
    }

    public async Task<bool> HasOtherActiveCharacters(Guid gameId, Guid userId, Guid excludeCharacterId)
    {
        return await _dbContext.Characters
            .AnyAsync(c => c.GameId == gameId &&
                           c.AuthorId == userId &&
                           c.CharacterId != excludeCharacterId &&
                           !c.IsRemoved &&
                           !c.IsNpc &&
                           c.Status == CharacterStatus.Active);
    }

    #endregion

    #region Write Operations

    public async Task<Character> Create(CreateCharacterEntity createCharacter)
    {
        var dbCharacter = new DbCharacter
        {
            CharacterId = createCharacter.CharacterId,
            GameId = createCharacter.GameId,
            AuthorId = createCharacter.AuthorId,
            Status = createCharacter.InitialStatus,
            CreatedUtc = createCharacter.CreatedUtc,
            Name = createCharacter.Name,
            IsNpc = createCharacter.IsNpc,
            AccessPolicy = createCharacter.AccessPolicy,
            IsRemoved = false
        };

        var attributes = createCharacter.Attributes.Select(a => new DbCharacterAttribute
        {
            CharacterAttributeId = _guidFactory.Create(),
            CharacterId = createCharacter.CharacterId,
            AttributeId = a.Id,
            Value = a.Value
        });

        _dbContext.Characters.Add(dbCharacter);
        _dbContext.CharacterAttributes.AddRange(attributes);
        await _dbContext.SaveChangesAsync();

        // Adjust PcLimit if non-NPC character is created as Active
        if (!createCharacter.IsNpc && createCharacter.InitialStatus == CharacterStatus.Active)
        {
            await AdjustPcLimitOnActivationAsync(createCharacter.GameId);
        }

        return await _dbContext.Characters
            .Where(c => c.CharacterId == createCharacter.CharacterId)
            .ProjectTo<Character>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <summary>
    /// Increases RecruitmentPcLimit if active players exceed it
    /// </summary>
    private async Task AdjustPcLimitOnActivationAsync(Guid gameId)
    {
        var game = await _dbContext.Games.FindAsync(gameId);
        if (game?.RecruitmentPcLimit == null)
            return;

        var activeCount = await _dbContext.Characters
            .CountAsync(c => c.GameId == gameId &&
                             !c.IsRemoved &&
                             !c.IsNpc &&
                             c.Status == CharacterStatus.Active);

        if (activeCount > game.RecruitmentPcLimit.Value)
        {
            game.RecruitmentPcLimit = activeCount;
            await _dbContext.SaveChangesAsync();
        }
    }

    public new async Task<Character> Update(UpdateCharacterEntity updateCharacter)
    {
        var character = await _dbContext.Characters.FindAsync(updateCharacter.CharacterId);
        if (character == null)
        {
            throw new InvalidOperationException($"Character {updateCharacter.CharacterId} not found");
        }

        // Track status change for PcLimit adjustment
        var oldStatus = character.Status;
        var newStatus = updateCharacter.Status ?? oldStatus;
        var isNpc = updateCharacter.IsNpc ?? character.IsNpc;

        // Update fields if provided
        if (updateCharacter.Status.HasValue)
            character.Status = updateCharacter.Status.Value;

        if (updateCharacter.IsDead.HasValue)
            character.IsDead = updateCharacter.IsDead.Value;

        if (updateCharacter.IsPlayerLeft.HasValue)
            character.IsPlayerLeft = updateCharacter.IsPlayerLeft.Value;

        if (updateCharacter.IsPlayerExiled.HasValue)
            character.IsPlayerExiled = updateCharacter.IsPlayerExiled.Value;

        if (!string.IsNullOrEmpty(updateCharacter.Name))
            character.Name = updateCharacter.Name;

        if (updateCharacter.IsNpc.HasValue)
            character.IsNpc = updateCharacter.IsNpc.Value;

        if (updateCharacter.AccessPolicy.HasValue)
            character.AccessPolicy = updateCharacter.AccessPolicy.Value;

        // Modification tracking is handled via Edit history, not inline ModifiedUtc

        // Update attributes
        if (updateCharacter.Attributes != null && updateCharacter.Attributes.Any())
        {
            var existingAttributeIds = await GetAttributeIds(updateCharacter.CharacterId);
            foreach (var attr in updateCharacter.Attributes)
            {
                if (existingAttributeIds.TryGetValue(attr.Id, out var existingId))
                {
                    var existingAttr = await _dbContext.CharacterAttributes.FindAsync(existingId);
                    if (existingAttr != null)
                        existingAttr.Value = attr.Value;
                }
                else
                {
                    _dbContext.CharacterAttributes.Add(new DbCharacterAttribute
                    {
                        CharacterAttributeId = _guidFactory.Create(),
                        CharacterId = updateCharacter.CharacterId,
                        AttributeId = attr.Id,
                        Value = attr.Value
                    });
                }
            }
        }

        await _dbContext.SaveChangesAsync();

        // Adjust RecruitmentPcLimit when non-NPC character status changes
        if (!isNpc && oldStatus != newStatus)
        {
            await AdjustPcLimitAsync(character.GameId, oldStatus, newStatus);
        }

        return await _dbContext.Characters
            .Where(c => c.CharacterId == updateCharacter.CharacterId)
            .ProjectTo<Character>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <summary>
    /// Raises the stated limit when a character joins the active roster
    /// </summary>
    /// <remarks>
    /// Only upwards, and only to the live count. Nothing reads
    /// RecruitmentPcLimit as a gate — whether recruitment is open is its own
    /// flag — so the number exists to be read beside the count of active
    /// characters, and the one state it must never be in is below it.
    ///
    /// A character leaving used to take the number down with it
    /// (Math.Max(activeCount, limit - 1)). Nothing needed that: it quietly
    /// rewrote what the master announced, so a game recruiting five players
    /// began claiming four the moment one left.
    /// </remarks>
    private async Task AdjustPcLimitAsync(Guid gameId, CharacterStatus oldStatus, CharacterStatus newStatus)
    {
        if (newStatus == CharacterStatus.Active && oldStatus != CharacterStatus.Active)
        {
            await AdjustPcLimitOnActivationAsync(gameId);
        }
    }

    public async Task Delete(Guid characterId, Guid deletedByUserId)
    {
        var character = await _dbContext.Characters.FindAsync(characterId);
        if (character != null)
        {
            SoftDelete.Mark(character, deletedByUserId, _dateTimeProvider.Now);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<int> DeclinePendingCharacters(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Characters
            .Where(c => !c.IsRemoved &&
                        c.GameId == gameId &&
                        c.AuthorId == userId &&
                        c.Status == CharacterStatus.UnderReview)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.Status, CharacterStatus.Declined), ct);
    }

    #endregion
}
