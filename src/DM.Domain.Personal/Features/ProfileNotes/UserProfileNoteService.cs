using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;

namespace DM.Domain.Personal.Features.ProfileNotes;

/// <inheritdoc />
internal class UserProfileNoteService : IUserProfileNoteService
{
    private readonly IUserProfileNoteRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public UserProfileNoteService(
        IUserProfileNoteRepository repository,
        IUserRepository userRepository,
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
    public async Task<UserProfileNote?> GetNote(string subjectUsername, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication required");
        }

        var subjectUser = await _userRepository.GetUserAsync(subjectUsername);
        if (subjectUser == null)
        {
            return null;
        }

        return await _repository.Get(currentUser.UserId, subjectUser.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<UserProfileNote?> UpsertNote(CreateUserProfileNote createNote, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication required");
        }

        var subjectUser = await _userRepository.GetUserAsync(createNote.SubjectUsername);
        if (subjectUser == null)
        {
            throw new ArgumentException($"User {createNote.SubjectUsername} not found");
        }

        if (subjectUser.UserId == currentUser.UserId)
        {
            throw new InvalidOperationException("Cannot create a note about yourself");
        }

        var existingNote = await _repository.Get(currentUser.UserId, subjectUser.UserId, ct);

        // Delete note if text is empty
        if (string.IsNullOrWhiteSpace(createNote.Text))
        {
            if (existingNote != null)
            {
                await _repository.Delete(existingNote.Id, ct);
            }
            return null;
        }

        var now = _dateTimeProvider.Now;

        if (existingNote != null)
        {
            var update = new UpdateUserProfileNoteEntity
            {
                Id = existingNote.Id,
                Text = createNote.Text,
                UpdatedUtc = now
            };
            return await _repository.Update(update, ct);
        }

        var entity = new CreateUserProfileNoteEntity
        {
            Id = _guidFactory.Create(),
            OwnerId = currentUser.UserId,
            SubjectUserId = subjectUser.UserId,
            Text = createNote.Text,
            CreatedUtc = now
        };

        return await _repository.Create(entity, ct);
    }

    /// <inheritdoc />
    public async Task DeleteNote(string subjectUsername, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication required");
        }

        var subjectUser = await _userRepository.GetUserAsync(subjectUsername);
        if (subjectUser == null)
        {
            throw new ArgumentException($"User {subjectUsername} not found");
        }

        var note = await _repository.Get(currentUser.UserId, subjectUser.UserId, ct);
        if (note != null)
        {
            await _repository.Delete(note.Id, ct);
        }
    }
}
