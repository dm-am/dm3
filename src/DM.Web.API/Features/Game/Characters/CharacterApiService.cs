using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Game.Features.Characters;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Characters;

/// <inheritdoc />
internal class CharacterApiService : ICharacterApiService
{
    private readonly ICharacterService _characterService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CharacterApiService(
        ICharacterService characterService,
        IMapper mapper)
    {
        _characterService = characterService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Character>> GetAll(Guid gameId)
    {
        var characters = await _characterService.GetAllAsync(gameId);
        return new ListEnvelope<Character>(characters.Select(_mapper.Map<Character>));
    }

    /// <inheritdoc />
    public async Task<Envelope<CharacterDetails>> Get(Guid characterId)
    {
        var character = await _characterService.GetAsync(characterId);
        return new Envelope<CharacterDetails>(_mapper.Map<CharacterDetails>(character));
    }

    /// <inheritdoc />
    public async Task<Envelope<CharacterDetails>> Create(Guid gameId, CharacterDetails character)
    {
        var createCharacter = _mapper.Map<CreateCharacter>(character);
        createCharacter.GameId = gameId;
        var createdCharacter = await _characterService.CreateAsync(createCharacter);
        return new Envelope<CharacterDetails>(_mapper.Map<CharacterDetails>(createdCharacter));
    }

    /// <inheritdoc />
    public async Task<Envelope<CharacterDetails>> Update(Guid characterId, CharacterDetails character)
    {
        var updateCharacter = _mapper.Map<UpdateCharacter>(character);
        updateCharacter.CharacterId = characterId;
        var updatedCharacter = await _characterService.UpdateAsync(updateCharacter);
        return new Envelope<CharacterDetails>(_mapper.Map<CharacterDetails>(updatedCharacter));
    }

    /// <inheritdoc />
    public Task Delete(Guid characterId) => _characterService.DeleteAsync(characterId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid gameId) => _characterService.MarkAsReadAsync(gameId);
}
