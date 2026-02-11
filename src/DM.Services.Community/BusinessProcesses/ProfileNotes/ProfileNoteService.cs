using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.ProfileNotes;

/// <inheritdoc />
internal class ProfileNoteService : IProfileNoteService
{
    private readonly IProfileNoteRepository _repository;
    private readonly IUserReadingRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public ProfileNoteService(
        IProfileNoteRepository repository,
        IUserReadingRepository userRepository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _userRepository = userRepository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<ProfileNoteDto?> GetNote(string subjectUserLogin, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication required");
        }

        var subjectUser = await _userRepository.GetUser(subjectUserLogin);
        if (subjectUser == null)
        {
            return null;
        }

        var note = await _repository.Get(currentUser.UserId, subjectUser.UserId, ct);
        if (note == null)
        {
            return null;
        }

        return MapToDto(note, subjectUserLogin);
    }

    /// <inheritdoc />
    public async Task<ProfileNoteDto> UpsertNote(CreateProfileNote createNote, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication required");
        }

        var subjectUser = await _userRepository.GetUser(createNote.SubjectUserLogin);
        if (subjectUser == null)
        {
            throw new ArgumentException($"User {createNote.SubjectUserLogin} not found");
        }

        if (subjectUser.UserId == currentUser.UserId)
        {
            throw new InvalidOperationException("Cannot create a note about yourself");
        }

        var existingNote = await _repository.Get(currentUser.UserId, subjectUser.UserId, ct);
        var now = _dateTimeProvider.Now;

        if (existingNote != null)
        {
            existingNote.Text = createNote.Text;
            existingNote.UpdatedUtc = now;
            var updated = await _repository.Update(existingNote, ct);
            return MapToDto(updated, createNote.SubjectUserLogin);
        }

        var note = new ProfileNote
        {
            NoteId = _guidFactory.Create(),
            OwnerId = currentUser.UserId,
            SubjectUserId = subjectUser.UserId,
            Text = createNote.Text,
            CreatedUtc = now
        };

        var created = await _repository.Create(note, ct);
        return MapToDto(created, createNote.SubjectUserLogin);
    }

    /// <inheritdoc />
    public async Task DeleteNote(string subjectUserLogin, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication required");
        }

        var subjectUser = await _userRepository.GetUser(subjectUserLogin);
        if (subjectUser == null)
        {
            throw new ArgumentException($"User {subjectUserLogin} not found");
        }

        var note = await _repository.Get(currentUser.UserId, subjectUser.UserId, ct);
        if (note != null)
        {
            await _repository.Delete(note.NoteId, ct);
        }
    }

    private static ProfileNoteDto MapToDto(ProfileNote note, string subjectUserLogin)
    {
        return new ProfileNoteDto
        {
            NoteId = note.NoteId,
            SubjectUserLogin = subjectUserLogin,
            SubjectUserId = note.SubjectUserId,
            Text = note.Text,
            CreatedUtc = note.CreatedUtc,
            UpdatedUtc = note.UpdatedUtc
        };
    }
}
