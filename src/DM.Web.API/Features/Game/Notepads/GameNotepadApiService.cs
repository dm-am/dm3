using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Notepads;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Notepads;

namespace DM.Web.API.Features.Game.Notepads;

public class GameNotepadApiService
{
    private readonly IGameNotepadService _notepadService;
    private readonly ICharacterService _characterService;
    private readonly NotepadMapper _mapper;

    public GameNotepadApiService(
        IGameNotepadService notepadService,
        ICharacterService characterService,
        NotepadMapper mapper)
    {
        _notepadService = notepadService;
        _characterService = characterService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<NotepadEntryResponse>> GetMasterEntries(Guid gameId)
    {
        var entries = await _notepadService.GetMasterEntries(gameId);
        return new ListEnvelope<NotepadEntryResponse>(
            entries.Select(_mapper.ToResponse).ToList());
    }

    public async Task<Envelope<NotepadEntryResponse>> CreateMasterEntry(Guid gameId, CreateNotepadEntryRequest request)
    {
        var createEntry = _mapper.ToCreateEntry(request);

        var entry = await _notepadService.CreateMasterEntry(gameId, createEntry);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    public async Task<ListEnvelope<NotepadEntryResponse>> GetCharacterNotepadEntries(Guid characterId)
    {
        var character = await _characterService.GetAsync(characterId);
        var entries = await _notepadService.GetPlayerEntries(character.GameId, characterId);
        return new ListEnvelope<NotepadEntryResponse>(
            entries.Select(_mapper.ToResponse).ToList());
    }

    public async Task<Envelope<NotepadEntryResponse>> CreateCharacterNotepadEntry(Guid characterId, CreateNotepadEntryRequest request)
    {
        var character = await _characterService.GetAsync(characterId);
        var entry = await _notepadService.CreatePlayerEntry(character.GameId, characterId, _mapper.ToCreateEntry(request));
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    public async Task<ListEnvelope<NotepadEntryResponse>> GetCharacterMasterNotepadEntries(Guid characterId)
    {
        var character = await _characterService.GetAsync(characterId);
        var entries = await _notepadService.GetCharacterMasterEntries(character.GameId, characterId);
        return new ListEnvelope<NotepadEntryResponse>(
            entries.Select(_mapper.ToResponse).ToList());
    }

    public async Task<Envelope<NotepadEntryResponse>> CreateCharacterMasterNotepadEntry(
        Guid characterId, CreateNotepadEntryRequest request)
    {
        var character = await _characterService.GetAsync(characterId);
        var createEntry = _mapper.ToCreateEntry(request);

        var entry = await _notepadService.CreateCharacterMasterEntry(character.GameId, characterId, createEntry);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    public async Task<Envelope<NotepadEntryResponse>> GetEntry(Guid entryId)
    {
        var entry = await _notepadService.GetEntry(entryId);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    public async Task<Envelope<NotepadEntryResponse>> UpdateEntry(Guid entryId, UpdateNotepadEntryRequest request)
    {
        var updateEntry = _mapper.ToUpdateEntry(request);

        var entry = await _notepadService.UpdateEntry(entryId, updateEntry);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    public Task DeleteEntry(Guid entryId) => _notepadService.DeleteEntry(entryId);
}
