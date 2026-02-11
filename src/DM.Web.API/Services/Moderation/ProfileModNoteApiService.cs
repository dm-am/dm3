using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Users.ModNotes;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Moderation;

namespace DM.Web.API.Services.Moderation;

/// <inheritdoc />
internal class ProfileModNoteApiService : IProfileModNoteApiService
{
    private readonly IProfileModNoteService _noteService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ProfileModNoteApiService(IProfileModNoteService noteService, IMapper mapper)
    {
        _noteService = noteService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ProfileModNote>> GetNotes(string login)
    {
        var notes = await _noteService.GetNotes(login);
        return new ListEnvelope<ProfileModNote>(notes.Select(_mapper.Map<ProfileModNote>));
    }

    /// <inheritdoc />
    public async Task<Envelope<ProfileModNote>> GetNote(Guid noteId)
    {
        var note = await _noteService.GetNote(noteId);
        return new Envelope<ProfileModNote>(_mapper.Map<ProfileModNote>(note));
    }

    /// <inheritdoc />
    public async Task<Envelope<ProfileModNote>> CreateNote(string login, CreateProfileModNoteRequest request)
    {
        var createNote = new CreateProfileModNote
        {
            UserLogin = login,
            Text = request.Text
        };

        var note = await _noteService.Create(createNote);
        return new Envelope<ProfileModNote>(_mapper.Map<ProfileModNote>(note));
    }

    /// <inheritdoc />
    public async Task<Envelope<ProfileModNote>> UpdateNote(Guid noteId, UpdateProfileModNoteRequest request)
    {
        var updateNote = new UpdateProfileModNote
        {
            NoteId = noteId,
            Text = request.Text
        };

        var note = await _noteService.Update(updateNote);
        return new Envelope<ProfileModNote>(_mapper.Map<ProfileModNote>(note));
    }

    /// <inheritdoc />
    public Task DeleteNote(Guid noteId)
    {
        return _noteService.Delete(noteId);
    }
}
