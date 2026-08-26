using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Notepads;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Notepads;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Notepads;

namespace DM.Web.API.Features.Game.Notepads;

/// <inheritdoc />
internal class GameNotepadApiService : IGameNotepadApiService
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

    #region Master Notepad

    /// <inheritdoc />
    public async Task<ListEnvelope<NotepadEntryResponse>> GetMasterEntries(Guid gameId)
    {
        var entries = await _notepadService.GetMasterEntries(gameId);
        return new ListEnvelope<NotepadEntryResponse>(
            entries.Select(_mapper.ToResponse).ToList());
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> CreateMasterEntry(Guid gameId, CreateNotepadEntryRequest request)
    {
        var createEntry = _mapper.ToCreateEntry(request);

        var entry = await _notepadService.CreateMasterEntry(gameId, createEntry);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    #endregion

    #region Player/Character Notepad

    /// <inheritdoc />
    public async Task<ListEnvelope<NotepadEntryResponse>> GetPlayerEntries(Guid gameId, Guid characterId)
    {
        var entries = await _notepadService.GetPlayerEntries(gameId, characterId);
        return new ListEnvelope<NotepadEntryResponse>(
            entries.Select(_mapper.ToResponse).ToList());
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> CreatePlayerEntry(Guid gameId, Guid characterId, CreateNotepadEntryRequest request)
    {
        var createEntry = _mapper.ToCreateEntry(request);

        var entry = await _notepadService.CreatePlayerEntry(gameId, characterId, createEntry);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    #endregion

    /// <inheritdoc />
    public async Task<ListEnvelope<NotepadEntryResponse>> GetCharacterNotepadEntries(Guid characterId)
    {
        var character = await _characterService.GetAsync(characterId);
        return await GetPlayerEntries(character.GameId, characterId);
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> CreateCharacterNotepadEntry(Guid characterId, CreateNotepadEntryRequest request)
    {
        var character = await _characterService.GetAsync(characterId);
        return await CreatePlayerEntry(character.GameId, characterId, request);
    }

    #region Character Master Notepad

    /// <inheritdoc />
    public async Task<ListEnvelope<NotepadEntryResponse>> GetCharacterMasterNotepadEntries(Guid characterId)
    {
        var character = await _characterService.GetAsync(characterId);
        var entries = await _notepadService.GetCharacterMasterEntries(character.GameId, characterId);
        return new ListEnvelope<NotepadEntryResponse>(
            entries.Select(_mapper.ToResponse).ToList());
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> CreateCharacterMasterNotepadEntry(
        Guid characterId, CreateNotepadEntryRequest request)
    {
        var character = await _characterService.GetAsync(characterId);
        var createEntry = _mapper.ToCreateEntry(request);

        var entry = await _notepadService.CreateCharacterMasterEntry(character.GameId, characterId, createEntry);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    #endregion

    #region Common Operations

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> GetEntry(Guid entryId)
    {
        var entry = await _notepadService.GetEntry(entryId);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> UpdateEntry(Guid entryId, UpdateNotepadEntryRequest request)
    {
        var updateEntry = _mapper.ToUpdateEntry(request);

        var entry = await _notepadService.UpdateEntry(entryId, updateEntry);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    /// <inheritdoc />
    public async Task DeleteEntry(Guid entryId)
    {
        await _notepadService.DeleteEntry(entryId);
    }

    #endregion
}
