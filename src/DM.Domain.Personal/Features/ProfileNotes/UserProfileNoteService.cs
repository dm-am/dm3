using FluentValidation;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
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
    private readonly IValidator<CreateUserProfileNote> _validator;

    /// <inheritdoc />
    public UserProfileNoteService(
        IUserProfileNoteRepository repository,
        IUserRepository userRepository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateUserProfileNote> validator)
    {
        _repository = repository;
        _userRepository = userRepository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _validator = validator;
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
        await _validator.ValidateAndThrowAsync(createNote, ct);
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication required");
        }

        var subjectUser = await _userRepository.GetUserAsync(createNote.SubjectUsername);
        if (subjectUser == null)
        {
            throw new HttpException(HttpStatusCode.NotFound,
                RefusalMessage.UserNotFoundByUsername(createNote.SubjectUsername));
        }

        if (subjectUser.UserId == currentUser.UserId)
        {
            throw new HttpException(HttpStatusCode.BadRequest,
                "Нельзя оставить заметку о себе");
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
                ModifiedUtc = now
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
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(subjectUsername));
        }

        var note = await _repository.Get(currentUser.UserId, subjectUser.UserId, ct);
        if (note != null)
        {
            await _repository.Delete(note.Id, ct);
        }
    }
}
