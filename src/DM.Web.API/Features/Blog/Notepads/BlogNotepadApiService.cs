using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Notepads;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Notepads;

namespace DM.Web.API.Features.Blog.Notepads;

public class BlogNotepadApiService
{
    private readonly IBlogNotepadService _notepadService;
    private readonly NotepadMapper _mapper;

    public BlogNotepadApiService(
        IBlogNotepadService notepadService,
        NotepadMapper mapper)
    {
        _notepadService = notepadService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<NotepadEntryResponse>> GetEntries(Guid blogId)
    {
        var entries = await _notepadService.GetEntries(blogId);
        return new ListEnvelope<NotepadEntryResponse>(
            entries.Select(_mapper.ToResponse).ToList());
    }

    public async Task<Envelope<NotepadEntryResponse>> GetEntry(Guid entryId)
    {
        var entry = await _notepadService.GetEntry(entryId);
        return new Envelope<NotepadEntryResponse>(_mapper.ToResponse(entry));
    }

    public async Task<Envelope<NotepadEntryResponse>> CreateEntry(Guid blogId, CreateNotepadEntryRequest request)
    {
        var createEntry = _mapper.ToCreateEntry(request);

        var entry = await _notepadService.CreateEntry(blogId, createEntry);
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
