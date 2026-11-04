using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.GameAttributeSchemas;

namespace DM.Domain.Game.Features.Characters;

public interface ICharacterRepository
{
    Task<GameAttributeSchema> GetGameSchema(Guid gameId);

    /// <summary>
    /// The sheet of the game the character belongs to
    /// </summary>
    Task<GameAttributeSchema> GetCharacterSchema(Guid characterId);

    Task<IEnumerable<Character>> GetCharacters(Guid gameId);

    Task<Character?> FindCharacter(Guid characterId);

    Task<CharacterToUpdate?> GetForUpdate(Guid characterId);

    Task<bool> HasOtherActiveCharacters(Guid gameId, Guid userId, Guid excludeCharacterId);

    Task<Character> Create(CreateCharacterEntity createCharacter);

    Task<Character> Update(UpdateCharacterEntity updateCharacter);

    Task Delete(Guid characterId, Guid deletedByUserId);

    Task<int> DeclinePendingCharacters(Guid gameId, Guid userId, CancellationToken ct = default);
}

internal static class CharacterRepositoryExtensions
{
    public static async Task<CharacterToUpdate> GetForUpdateOrThrow(this ICharacterRepository repository, Guid characterId) =>
        await repository.GetForUpdate(characterId) ??
        throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CharacterNotFound);
}
