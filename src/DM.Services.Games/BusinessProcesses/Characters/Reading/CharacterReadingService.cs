using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Gaming.BusinessProcesses.Characters.Shared;
using DM.Services.Gaming.BusinessProcesses.Games.Reading;
using DM.Services.Gaming.Dto.Output;

namespace DM.Services.Gaming.BusinessProcesses.Characters.Reading;

/// <inheritdoc />
internal class CharacterReadingService : ICharacterReadingService
{
    private readonly IGameReadingService _gameReadingService;
    private readonly ICharacterReadingRepository _readingRepository;
    private readonly ICharacterAttributeValueFiller _attributeValueFiller;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public CharacterReadingService(
        IGameReadingService gameReadingService,
        ICharacterReadingRepository readingRepository,
        ICharacterAttributeValueFiller attributeValueFiller,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider)
    {
        _gameReadingService = gameReadingService;
        _readingRepository = readingRepository;
        _attributeValueFiller = attributeValueFiller;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Character>> GetCharacters(Guid gameId)
    {
        var game = await _gameReadingService.GetGame(gameId);
        var characters = (await _readingRepository.GetCharacters(gameId)).ToArray();
        await _attributeValueFiller.Fill(characters, game.AttributeSchemaId);
        return characters;
    }

    /// <inheritdoc />
    public async Task<Character> GetCharacter(Guid characterId)
    {
        var character = await _readingRepository.FindCharacter(characterId);
        if (character == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Character not found");
        }

        var game = await _gameReadingService.GetGame(character.GameId);
        await _attributeValueFiller.Fill(new[] {character}, game.AttributeSchemaId);
        return character;
    }

    /// <inheritdoc />
    public async Task MarkAsRead(Guid gameId)
    {
        await _gameReadingService.GetGame(gameId);
        await _unreadCountersRepository.Flush(_identityProvider.Current.User.UserId,
            UnreadEntryType.Character, gameId);
    }
}