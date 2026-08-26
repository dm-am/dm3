using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Notepads;
using DM.Domain.Personal.Features.Notepads;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Personal.Notepads;

/// <inheritdoc />
internal class UserNotepadApiService : IUserNotepadApiService
{
    private readonly IUserNotepadService _notepadService;
    private readonly NotepadMapper _mapper;

    public UserNotepadApiService(
        IUserNotepadService notepadService,
        NotepadMapper mapper)
    {
        _notepadService = notepadService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<NotepadEntryResponse>> GetEntries()
    {
        var entries = await _notepadService.GetEntries();
        return new ListEnvelope<NotepadEntryResponse>(
            entries.Select(_mapper.ToResponse).ToList());
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> GetEntry(Guid entryId)
    {
        var entry = await _notepadService.GetEntry(entryId);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> CreateEntry(CreateNotepadEntryRequest request)
    {
        var createEntry = _mapper.ToCreateEntry(request);

        var entry = await _notepadService.CreateEntry(createEntry);
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
}
