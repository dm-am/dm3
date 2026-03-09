using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Blog.Features.Notepads;
using DM.Domain.Core.Notepads;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Notepads;

namespace DM.Web.API.Features.Blog.Notepads;

/// <inheritdoc />
internal class BlogNotepadApiService : IBlogNotepadApiService
{
    private readonly IBlogNotepadService _notepadService;
    private readonly IMapper _mapper;

    public BlogNotepadApiService(
        IBlogNotepadService notepadService,
        IMapper mapper)
    {
        _notepadService = notepadService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<NotepadEntryResponse>> GetEntries(Guid blogId)
    {
        var entries = await _notepadService.GetEntries(blogId);
        return new ListEnvelope<NotepadEntryResponse>(
            entries.Select(_mapper.Map<NotepadEntryResponse>).ToList());
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> GetEntry(Guid entryId)
    {
        var entry = await _notepadService.GetEntry(entryId);
        return new Envelope<NotepadEntryResponse>(_mapper.Map<NotepadEntryResponse>(entry));
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> CreateEntry(Guid blogId, CreateNotepadEntryRequest request)
    {
        var createEntry = new CreateNotepadEntry
        {
            CategoryId = request.CategoryId,
            Title = request.Title,
            Content = request.Content
        };

        var entry = await _notepadService.CreateEntry(blogId, createEntry);
        return new Envelope<NotepadEntryResponse>(_mapper.Map<NotepadEntryResponse>(entry));
    }

    /// <inheritdoc />
    public async Task<Envelope<NotepadEntryResponse>> UpdateEntry(Guid entryId, UpdateNotepadEntryRequest request)
    {
        var updateEntry = new UpdateNotepadEntry
        {
            CategoryId = request.CategoryId,
            Title = request.Title,
            Content = request.Content,
            SortOrder = request.SortOrder
        };

        var entry = await _notepadService.UpdateEntry(entryId, updateEntry);
        return new Envelope<NotepadEntryResponse>(_mapper.Map<NotepadEntryResponse>(entry));
    }

    /// <inheritdoc />
    public async Task DeleteEntry(Guid entryId)
    {
        await _notepadService.DeleteEntry(entryId);
    }
}
