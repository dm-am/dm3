using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.ProfileNotes;

namespace DM.Web.API.Features.Moderation.Profiles;

public class ModeratedProfileNoteApiService
{
    private readonly IModeratedProfileNoteService _noteService;
    private readonly ModeratedProfileNoteMapper _mapper;

    public ModeratedProfileNoteApiService(IModeratedProfileNoteService noteService, ModeratedProfileNoteMapper mapper)
    {
        _noteService = noteService;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ModeratedProfileNote>> GetNotes(string username)
    {
        var notes = await _noteService.GetNotes(username);
        return notes.Select(_mapper.ToNote);
    }

    public async Task<ModeratedProfileNote> GetNote(Guid noteId)
    {
        var note = await _noteService.GetNote(noteId);
        return _mapper.ToNote(note);
    }

    public async Task<ModeratedProfileNote> CreateNote(string username, CreateModeratedProfileNoteRequest request)
    {
        var note = await _noteService.Create(new CreateModeratedProfileNote { Username = username, Text = request.Text });
        return _mapper.ToNote(note);
    }

    public async Task<ModeratedProfileNote> UpdateNote(Guid noteId, UpdateModeratedProfileNoteRequest request)
    {
        var note = await _noteService.Update(new UpdateModeratedProfileNote { Id = noteId, Text = request.Text });
        return _mapper.ToNote(note);
    }

    public Task DeleteNote(Guid noteId) => _noteService.Delete(noteId);
}
