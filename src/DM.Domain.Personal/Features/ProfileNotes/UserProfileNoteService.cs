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
        // A personal note is one the viewer wrote, so an anonymous viewer has
        // none — null is the answer the nullable return type already promises.
        // Throwing here made the profile page catch an exception to learn that,
        // which is control flow across a layer boundary and the reason the
        // refusal never reached anyone as a 401 either.
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
        {
            return null;
        }

        // Only the identifier is needed to find the note, and GetUserAsync pays for
        // the whole achievement profile to hand it over.
        var subjectUserId = await _userRepository.FindUserIdAsync(subjectUsername);
        if (subjectUserId == null)
        {
            return null;
        }

        return await _repository.Get(currentUser.UserId, subjectUserId.Value, ct);
    }

    /// <inheritdoc />
    public async Task<UserProfileNote?> UpsertNote(CreateUserProfileNote createNote, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(createNote, ct);
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
        {
            throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
        }

        var subjectUserId = await _userRepository.FindUserIdAsync(createNote.SubjectUsername);
        if (subjectUserId == null)
        {
            throw new HttpException(HttpStatusCode.NotFound,
                RefusalMessage.UserNotFoundByUsername(createNote.SubjectUsername));
        }

        if (subjectUserId.Value == currentUser.UserId)
        {
            throw new HttpException(HttpStatusCode.BadRequest,
                "Нельзя оставить заметку о себе");
        }

        // Delete note if text is empty
        if (string.IsNullOrWhiteSpace(createNote.Text))
        {
            var existingNote = await _repository.Get(currentUser.UserId, subjectUserId.Value, ct);
            if (existingNote != null)
            {
                await _repository.Delete(existingNote.Id, ct);
            }
            return null;
        }

        // No read before the write: whether the pair already has a note is
        // decided by the write itself. A read here stops being true the moment
        // it returns - two saves arriving together both found nothing and both
        // inserted, and a save that found the note lost it to a clear arriving
        // with it and failed to update a row that was gone. Both answered 500.
        var entity = new CreateUserProfileNoteEntity
        {
            Id = _guidFactory.Create(),
            OwnerId = currentUser.UserId,
            SubjectUserId = subjectUserId.Value,
            Text = createNote.Text,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.Create(entity, ct);
    }

    /// <inheritdoc />
    public async Task DeleteNote(string subjectUsername, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
        {
            throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
        }

        var subjectUserId = await _userRepository.FindUserIdAsync(subjectUsername);
        if (subjectUserId == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(subjectUsername));
        }

        var note = await _repository.Get(currentUser.UserId, subjectUserId.Value, ct);
        if (note != null)
        {
            await _repository.Delete(note.Id, ct);
        }
    }
}
