using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notepads;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Personal.Notepads;

public class UserNotepadApiService
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

    public async Task<ListEnvelope<NotepadEntryResponse>> GetEntries()
    {
        var entries = await _notepadService.GetEntries();
        return new ListEnvelope<NotepadEntryResponse>(
            entries.Select(_mapper.ToResponse).ToList());
    }

    public async Task<Envelope<NotepadEntryResponse>> GetEntry(Guid entryId)
    {
        var entry = await _notepadService.GetEntry(entryId);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    public async Task<Envelope<NotepadEntryResponse>> CreateEntry(CreateNotepadEntryRequest request)
    {
        var entry = await _notepadService.CreateEntry(_mapper.ToCreateEntry(request));
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    public async Task<Envelope<NotepadEntryResponse>> UpdateEntry(Guid entryId, UpdateNotepadEntryRequest request)
    {
        var entry = await _notepadService.UpdateEntry(entryId, _mapper.ToUpdateEntry(request));
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    public Task DeleteEntry(Guid entryId) => _notepadService.DeleteEntry(entryId);
}
