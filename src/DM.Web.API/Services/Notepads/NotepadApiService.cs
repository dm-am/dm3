using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Core.Dto.Enums;
using DM.Services.Game.BusinessProcesses.Characters.Reading;
using DM.Services.Game.BusinessProcesses.Notepads;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notepads;

namespace DM.Web.API.Services.Notepads;

/// <inheritdoc />
internal class NotepadApiService : INotepadApiService
{
    private readonly INotepadService _notepadService;
    private readonly ICharacterReadingService _characterReadingService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public NotepadApiService(
        INotepadService notepadService,
        ICharacterReadingService characterReadingService,
        IMapper mapper)
    {
        _notepadService = notepadService;
        _characterReadingService = characterReadingService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<NotepadEntry>> GetGameMasterEntries(Guid gameId)
    {
        var entries = await _notepadService.GetGameMasterEntries(gameId);
        return new ListEnvelope<NotepadEntry>(entries.Select(_mapper.Map<NotepadEntry>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<NotepadEntry>> GetPlayerEntries(Guid gameId, Guid characterId)
    {
        var entries = await _notepadService.GetPlayerEntries(gameId, characterId);
        return new ListEnvelope<NotepadEntry>(entries.Select(_mapper.Map<NotepadEntry>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<NotepadEntry>> GetCharacterNotepadEntries(Guid characterId)
    {
        var character = await _characterReadingService.GetCharacter(characterId);
        return await GetPlayerEntries(character.GameId, characterId);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<NotepadEntry>> GetUserEntries()
    {
        var entries = await _notepadService.GetUserEntries();
        return new ListEnvelope<NotepadEntry>(entries.Select(_mapper.Map<NotepadEntry>));
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntry>> GetEntry(Guid entryId)
    {
        var entry = await _notepadService.GetEntry(entryId);
        return new Envelope<NotepadEntry>(_mapper.Map<NotepadEntry>(entry));
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntry>> CreateGameMasterEntry(Guid gameId, CreateNotepadEntryRequest request)
    {
        var createEntry = new CreateNotepadEntry
        {
            NotepadType = NotepadType.Master,
            ContainerId = gameId,
            OwnerId = null,
            CategoryId = request.CategoryId,
            Title = request.Title,
            Content = request.Content
        };

        var entry = await _notepadService.CreateEntry(createEntry);
        return new Envelope<NotepadEntry>(_mapper.Map<NotepadEntry>(entry));
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntry>> CreatePlayerEntry(Guid gameId, Guid characterId, CreateNotepadEntryRequest request)
    {
        var createEntry = new CreateNotepadEntry
        {
            NotepadType = NotepadType.Player,
            ContainerId = gameId,
            OwnerId = characterId,
            CategoryId = request.CategoryId,
            Title = request.Title,
            Content = request.Content
        };

        var entry = await _notepadService.CreateEntry(createEntry);
        return new Envelope<NotepadEntry>(_mapper.Map<NotepadEntry>(entry));
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntry>> CreateCharacterNotepadEntry(Guid characterId, CreateNotepadEntryRequest request)
    {
        var character = await _characterReadingService.GetCharacter(characterId);
        return await CreatePlayerEntry(character.GameId, characterId, request);
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntry>> CreateUserEntry(CreateNotepadEntryRequest request)
    {
        var createEntry = new CreateNotepadEntry
        {
            NotepadType = NotepadType.User,
            ContainerId = Guid.Empty, // Will be set to user ID in service
            OwnerId = null,
            CategoryId = request.CategoryId,
            Title = request.Title,
            Content = request.Content
        };

        var entry = await _notepadService.CreateEntry(createEntry);
        return new Envelope<NotepadEntry>(_mapper.Map<NotepadEntry>(entry));
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntry>> UpdateEntry(Guid entryId, UpdateNotepadEntryRequest request)
    {
        var updateEntry = new UpdateNotepadEntry
        {
            CategoryId = request.CategoryId,
            Title = request.Title,
            Content = request.Content,
            SortOrder = request.SortOrder
        };

        var entry = await _notepadService.UpdateEntry(entryId, updateEntry);
        return new Envelope<NotepadEntry>(_mapper.Map<NotepadEntry>(entry));
    }

    /// <inheritdoc />
    public async Task DeleteEntry(Guid entryId)
    {
        await _notepadService.DeleteEntry(entryId);
    }
}
