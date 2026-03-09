using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Moderation.Features.ProfileNotes;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <inheritdoc />
internal class ModeratedProfileNoteApiService : IModeratedProfileNoteApiService
{
    private readonly IModeratedProfileNoteService _noteService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ModeratedProfileNoteApiService(IModeratedProfileNoteService noteService, IMapper mapper)
    {
        _noteService = noteService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModeratedProfileNote>> GetNotes(string username)
    {
        var notes = await _noteService.GetNotes(username);
        return notes.Select(_mapper.Map<ModeratedProfileNote>);
    }

    /// <inheritdoc />
    public async Task<ModeratedProfileNote> GetNote(Guid noteId)
    {
        var note = await _noteService.GetNote(noteId);
        return _mapper.Map<ModeratedProfileNote>(note);
    }

    /// <inheritdoc />
    public async Task<ModeratedProfileNote> CreateNote(string username, CreateModeratedProfileNoteRequest request)
    {
        var createNote = new CreateModeratedProfileNote
        {
            Username = username,
            Text = request.Text
        };

        var note = await _noteService.Create(createNote);
        return _mapper.Map<ModeratedProfileNote>(note);
    }

    /// <inheritdoc />
    public async Task<ModeratedProfileNote> UpdateNote(Guid noteId, UpdateModeratedProfileNoteRequest request)
    {
        var updateNote = new UpdateModeratedProfileNote
        {
            NoteId = noteId,
            Text = request.Text
        };

        var note = await _noteService.Update(updateNote);
        return _mapper.Map<ModeratedProfileNote>(note);
    }

    /// <inheritdoc />
    public Task DeleteNote(Guid noteId)
    {
        return _noteService.Delete(noteId);
    }
}
