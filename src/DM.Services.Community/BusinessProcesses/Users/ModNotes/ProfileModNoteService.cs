using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Users.ModNotes;

/// <inheritdoc />
internal class ProfileModNoteService : IProfileModNoteService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IUserReadingRepository _userRepository;
    private readonly IProfileModNoteRepository _noteRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ProfileModNoteService(
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IUserReadingRepository userRepository,
        IProfileModNoteRepository noteRepository,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        IMapper mapper)
    {
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _userRepository = userRepository;
        _noteRepository = noteRepository;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProfileModNoteDto>> GetNotes(string userLogin)
    {
        var user = await _userRepository.GetUser(userLogin)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"User {userLogin} not found");

        _intentionManager.ThrowIfForbidden(UserIntention.ReadModNotes, user);

        var notes = await _noteRepository.GetNotes(user.UserId);
        return notes.Select(n => _mapper.Map<ProfileModNoteDto>(n));
    }

    /// <inheritdoc />
    public async Task<ProfileModNoteDto> GetNote(Guid noteId)
    {
        var note = await _noteRepository.GetNote(noteId)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Note {noteId} not found");

        var user = await _userRepository.GetUser(note.UserId)
            ?? throw new HttpException(HttpStatusCode.NotFound, "User not found");

        _intentionManager.ThrowIfForbidden(UserIntention.ReadModNotes, user);

        return _mapper.Map<ProfileModNoteDto>(note);
    }

    /// <inheritdoc />
    public async Task<ProfileModNoteDto> Create(CreateProfileModNote createNote)
    {
        var user = await _userRepository.GetUser(createNote.UserLogin)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"User {createNote.UserLogin} not found");

        _intentionManager.ThrowIfForbidden(UserIntention.CreateModNote, user);

        var identity = _identityProvider.Current;
        var note = new ProfileModNote
        {
            ProfileModNoteId = _guidFactory.Create(),
            UserId = user.UserId,
            AuthorId = identity.User.UserId,
            Text = createNote.Text,
            CreatedAtUtc = _dateTimeProvider.Now,
            IsRemoved = false
        };

        var created = await _noteRepository.Create(note);
        return _mapper.Map<ProfileModNoteDto>(created);
    }

    /// <inheritdoc />
    public async Task<ProfileModNoteDto> Update(UpdateProfileModNote updateNote)
    {
        var note = await _noteRepository.GetNote(updateNote.NoteId)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Note {updateNote.NoteId} not found");

        var user = await _userRepository.GetUser(note.UserId)
            ?? throw new HttpException(HttpStatusCode.NotFound, "User not found");

        _intentionManager.ThrowIfForbidden(UserIntention.EditModNote, user);

        // Check if user can edit this specific note
        var identity = _identityProvider.Current;
        if (note.AuthorId != identity.User.UserId && identity.User.Role < UserRole.SeniorModerator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You can only edit your own notes");
        }

        note.Text = updateNote.Text;
        note.ModifiedAtUtc = _dateTimeProvider.Now;

        await _noteRepository.Update(note);

        var updated = await _noteRepository.GetNote(updateNote.NoteId);
        return _mapper.Map<ProfileModNoteDto>(updated!);
    }

    /// <inheritdoc />
    public async Task Delete(Guid noteId)
    {
        var note = await _noteRepository.GetNote(noteId)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Note {noteId} not found");

        var user = await _userRepository.GetUser(note.UserId)
            ?? throw new HttpException(HttpStatusCode.NotFound, "User not found");

        _intentionManager.ThrowIfForbidden(UserIntention.DeleteModNote, user);

        // Check if user can delete this specific note
        var identity = _identityProvider.Current;
        if (note.AuthorId != identity.User.UserId && identity.User.Role < UserRole.SeniorModerator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You can only delete your own notes");
        }

        await _noteRepository.Delete(noteId);
    }
}
