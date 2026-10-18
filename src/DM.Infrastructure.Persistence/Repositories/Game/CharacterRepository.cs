using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
/// <remarks>
/// The character sheet of a game is its own aggregate, and it is read through
/// the repository that owns it rather than through a second view of it opened
/// here: the class used to hold a second reading of the schema store for one
/// read, free to disagree with the owner about what the schema holds.
/// </remarks>
internal class CharacterRepository : ICharacterRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGameAttributeSchemaRepository _gameSchemas;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public CharacterRepository(
        DmDbContext dbContext,
        IGameAttributeSchemaRepository gameSchemas,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _gameSchemas = gameSchemas;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    #region Validation Operations

    public Task<GameAttributeSchema> GetGameSchema(Guid gameId) =>
        _gameSchemas.Get(gameId);

    public async Task<GameAttributeSchema> GetCharacterSchema(Guid characterId)
    {
        var gameId = await _dbContext.Characters
            .Where(c => c.CharacterId == characterId)
            .Select(c => c.GameId)
            .FirstAsync();

        return await _gameSchemas.Get(gameId);
    }

    #endregion

    #region Read Operations

    public async Task<IEnumerable<Character>> GetCharacters(Guid gameId)
    {
        return await _dbContext.Characters
            .Where(c => !c.IsRemoved && c.GameId == gameId)
            .OrderByDescending(c => c.CreatedUtc)
            .ProjectToCharacter()
            .ToArrayAsync();
    }

    public async Task<Character?> FindCharacter(Guid characterId)
    {
        return await _dbContext.Characters
            .Where(c => !c.IsRemoved && c.CharacterId == characterId)
            .ProjectToCharacter()
            .FirstOrDefaultAsync();
    }

    public Task<CharacterToUpdate?> GetForUpdate(Guid characterId)
    {
        return _dbContext.Characters
            .Where(c => c.CharacterId == characterId)
            .ProjectToCharacterToUpdate()
            .FirstOrDefaultAsync<CharacterToUpdate?>();
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

        // Whether this character joins the active roster, and therefore whether
        // the stated limit can be moved by writing it. Answered from what the
        // caller passed rather than from a read of the game or of the roster: the
        // answer decides whether the game row is taken, so a read that decided it
        // would be a read the lock is supposed to come before.
        var joinsActiveRoster =
            !createCharacter.IsNpc && createCharacter.InitialStatus == CharacterStatus.Active;

        // Both writes or neither: separately, a refusal between them left a game
        // with more active characters than the limit it announces, and nothing
        // recomputes that number afterwards.
        //
        // Through the strategy because the API host configures EnableRetryOnFailure and
        // a retrying strategy refuses a transaction opened by hand. The entities are
        // built above so their identifiers survive a retry unchanged.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // The stated limit is decided by counting the active roster, and a
            // count cannot see the character another transaction has inserted but
            // not committed: two characters activated at once each counted only
            // itself, each found the limit large enough, and the game was left
            // announcing fewer players than it has. Locking the game row first
            // serializes the decision per game, and the count below then runs as a
            // separate statement after the wait, so it sees what the winner wrote.
            //
            // Only for a character that joins that roster. A draft, an application
            // and an NPC change no number the game announces, and taking the game
            // row for them serializes every character of a game against every
            // other - a master editing an NPC waited out an acceptance that has
            // nothing to do with him.
            if (joinsActiveRoster)
            {
                await LockGameAsync(createCharacter.GameId);
            }

            _dbContext.Characters.Add(dbCharacter);
            _dbContext.CharacterAttributes.AddRange(attributes);
            await _dbContext.SaveChangesAsync();

            // Adjust PcLimit if non-NPC character is created as Active
            if (joinsActiveRoster)
            {
                await AdjustPcLimitOnActivationAsync(createCharacter.GameId);
            }

            await transaction.CommitAsync();
        });

        return await _dbContext.Characters
            .Where(c => c.CharacterId == createCharacter.CharacterId)
            .ProjectToCharacter()
            .FirstAsync();
    }

    /// <summary>
    /// Locks the game row for the rest of the transaction.
    /// </summary>
    /// <remarks>
    /// Taken as the first statement of the transaction, before anything the
    /// decision depends on is read. A lock taken later buys nothing: a statement
    /// that waited on it still answers from the snapshot it started with and does
    /// not see what the transaction it waited for has committed.
    /// </remarks>
    private Task LockGameAsync(Guid gameId) => _dbContext.Database.ExecuteSqlRawAsync(
        """SELECT "GameId" FROM "Games" WHERE "GameId" = {0} FOR UPDATE""", gameId);

    /// <summary>
    /// Writes one attribute value of a character, whether or not it already had
    /// one.
    /// </summary>
    /// <remarks>
    /// Settled by the unique pair the table already carries, so the decision
    /// belongs to the server and not to a read this side took beforehand. The
    /// identifier of a brand new row is minted here and thrown away on a
    /// conflict, which costs nothing: nobody addresses these rows by it.
    /// </remarks>
    private Task UpsertAttribute(Guid characterId, Guid attributeId, string value) =>
        _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CharacterAttributes" ("CharacterAttributeId", "CharacterId", "AttributeId", "Value")
            VALUES ({_guidFactory.Create()}, {characterId}, {attributeId}, {value})
            ON CONFLICT ("CharacterId", "AttributeId") DO UPDATE SET "Value" = EXCLUDED."Value"
            """);

    /// <summary>
    /// Locks the game of a character for the rest of the transaction.
    /// </summary>
    /// <remarks>
    /// An update names the character and nothing else, and the game it belongs to
    /// never changes, so the identifier is looked up inside the locking statement
    /// rather than by a read of its own that would have to run before the lock.
    /// </remarks>
    private Task LockGameOfCharacterAsync(Guid characterId) => _dbContext.Database.ExecuteSqlRawAsync(
        """
        SELECT "GameId" FROM "Games"
        WHERE "GameId" = (SELECT "GameId" FROM "Characters" WHERE "CharacterId" = {0})
        FOR UPDATE
        """, characterId);

    /// <summary>
    /// Increases RecruitmentPcLimit if active players exceed it
    /// </summary>
    /// <remarks>
    /// Reached only from the branch that took the game row first, so both reads
    /// below answer for the roster as it stands once every competing activation
    /// has committed - the two conditions are the same one, written once as
    /// "this character joins the active roster". The limit is
    /// read by a query rather than by Find, and written set-based rather than
    /// through the tracker: the same DbContext serves the whole request and may
    /// already hold a copy of this game read before the lock, and Find would hand
    /// that copy back and decide against a number the wait has already changed.
    /// </remarks>
    private async Task AdjustPcLimitOnActivationAsync(Guid gameId)
    {
        var statedLimit = await _dbContext.Games
            .AsNoTracking()
            .Where(g => g.GameId == gameId)
            .Select(g => g.RecruitmentPcLimit)
            .FirstOrDefaultAsync();
        if (statedLimit == null)
            return;

        var activeCount = await _dbContext.Characters
            .CountAsync(c => c.GameId == gameId &&
                             !c.IsRemoved &&
                             !c.IsNpc &&
                             c.Status == CharacterStatus.Active);

        if (activeCount > statedLimit.Value)
        {
            await _dbContext.Games
                .Where(g => g.GameId == gameId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(g => g.RecruitmentPcLimit, activeCount));
        }
    }

    public async Task<Character> Update(UpdateCharacterEntity updateCharacter)
    {
        // The one status change that can move the stated limit is a character
        // becoming active. Read from the request alone, as in Create: whether the
        // game row is taken cannot depend on a read the lock has to precede.
        // Whether the character is an NPC and what status it holds now are read
        // below, under the lock, and narrow the decision further.
        var mayJoinActiveRoster = updateCharacter.Status == CharacterStatus.Active;

        // Both writes or neither, for the same reason as Create above. The read is
        // inside the block: cleared out of the tracker by a retry, an entity read
        // outside it would take every edit with it.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // Same reason as in Create: the stated limit is decided by counting the
            // active roster, and two applications accepted at once each counted
            // only itself. The lock is the first statement of the transaction, so
            // every read the decision rests on is taken after the wait. An edit
            // that cannot reach the active roster - a rename, an attribute, a
            // character being declined - takes nothing and waits for nobody.
            if (mayJoinActiveRoster)
            {
                await LockGameOfCharacterAsync(updateCharacter.CharacterId);
            }

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

            await _dbContext.SaveChangesAsync();

            // The values, as upserts settled by the unique pair rather than by a
            // read this method took first. Replacing the sheet of a game deletes
            // the rows of every specification it dropped, and that delete can
            // land between a read here and the write it decided: the read then
            // said "update", the update reached no row, and EF answered the
            // player with a failure over a value they were allowed to write.
            // Now either direction of that race ends the same way - one orphan
            // row until the next replacement, and the player's save accepted.
            if (updateCharacter.Attributes != null && updateCharacter.Attributes.Any())
            {
                foreach (var attr in updateCharacter.Attributes)
                {
                    await UpsertAttribute(updateCharacter.CharacterId, attr.Id, attr.Value);
                }
            }

            // Adjust RecruitmentPcLimit when non-NPC character status changes
            if (!isNpc && oldStatus != newStatus)
            {
                await AdjustPcLimitAsync(character.GameId, oldStatus, newStatus);
            }

            await transaction.CommitAsync();
        });

        return await _dbContext.Characters
            .Where(c => c.CharacterId == updateCharacter.CharacterId)
            .ProjectToCharacter()
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
