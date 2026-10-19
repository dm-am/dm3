using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Characters;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Characters;

public class CharacterApiService
{
    private readonly ICharacterService _characterService;
    private readonly CharacterMapper _mapper;

    public CharacterApiService(
        ICharacterService characterService,
        CharacterMapper mapper)
    {
        _characterService = characterService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<Character>> GetAll(Guid gameId)
    {
        var characters = await _characterService.GetAllAsync(gameId);
        return new ListEnvelope<Character>(characters.Select(_mapper.ToCharacter));
    }

    public async Task<Envelope<CharacterDetails>> Get(Guid characterId)
    {
        var character = await _characterService.GetAsync(characterId);
        return new Envelope<CharacterDetails>(
            _mapper.ToCharacterDetails(character, await _characterService.MayEditAsync(characterId)));
    }

    public async Task<Envelope<CharacterDetails>> Create(Guid gameId, CharacterDetails character)
    {
        var createCharacter = CharacterMapper.ToCreateCharacter(character);
        createCharacter.GameId = gameId;
        var createdCharacter = await _characterService.CreateAsync(createCharacter);
        return await Refetch(createdCharacter.Id);
    }

    public async Task<Envelope<CharacterDetails>> Update(Guid characterId, UpdateCharacterRequest request)
    {
        var updateCharacter = CharacterMapper.ToUpdateCharacter(request);
        updateCharacter.CharacterId = characterId;
        var updatedCharacter = await _characterService.UpdateAsync(updateCharacter);
        return await Refetch(updatedCharacter.Id);
    }

    public async Task<Envelope<CharacterDetails>> ChangeStatus(
        Guid characterId, CharacterStatusChangeRequest request)
    {
        var changed = await _characterService.ChangeStatusAsync(characterId, request.Transition);
        return await Refetch(changed.Id);
    }

    /// <summary>
    /// A write answers through the read path, so the attribute values come back
    /// filled and redacted as a GET returns them.
    /// </summary>
    private async Task<Envelope<CharacterDetails>> Refetch(Guid characterId) =>
        new(_mapper.ToCharacterDetails(
            await _characterService.GetAsync(characterId), await _characterService.MayEditAsync(characterId)));

    public Task Delete(Guid characterId) => _characterService.DeleteAsync(characterId);

    public Task MarkAsRead(Guid gameId) => _characterService.MarkAsReadAsync(gameId);
}
