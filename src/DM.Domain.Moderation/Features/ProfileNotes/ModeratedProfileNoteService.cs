using FluentValidation;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Authorization;

namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <inheritdoc />
internal class ModeratedProfileNoteService : IModeratedProfileNoteService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IUserLookupService _userLookupService;
    private readonly IModeratedProfileNoteRepository _noteRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IValidator<CreateModeratedProfileNote> _createValidator;
    private readonly IValidator<UpdateModeratedProfileNote> _updateValidator;

    public ModeratedProfileNoteService(
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IUserLookupService userLookupService,
        IModeratedProfileNoteRepository noteRepository,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        IValidator<CreateModeratedProfileNote> createValidator,
        IValidator<UpdateModeratedProfileNote> updateValidator)
    {
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _userLookupService = userLookupService;
        _noteRepository = noteRepository;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModeratedProfileNote>> GetNotes(string username)
    {
        var user = await _userLookupService.GetAsync(username);

        _intentionManager.ThrowIfForbidden(ModerationIntention.ViewModNotes);

        return await _noteRepository.GetNotes(user.UserId);
    }

    /// <inheritdoc />
    public async Task<ModeratedProfileNote> GetNote(Guid noteId)
    {
        var note = await GetExistingNote(noteId);

        _intentionManager.ThrowIfForbidden(ModerationIntention.ViewModNotes);

        return note;
    }

    /// <inheritdoc />
    public async Task<ModeratedProfileNote> Create(CreateModeratedProfileNote createNote)
    {
        await _createValidator.ValidateAndThrowAsync(createNote);
        var user = await _userLookupService.GetAsync(createNote.Username);

        _intentionManager.ThrowIfForbidden(ModerationIntention.CreateModNote);

        var identity = _identityProvider.Current;
        var entity = new CreateModeratedProfileNoteEntity
        {
            Id = _guidFactory.Create(),
            UserId = user.UserId,
            AuthorId = identity.User.UserId,
            Text = createNote.Text,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _noteRepository.Create(entity);
    }

    /// <inheritdoc />
    public async Task<ModeratedProfileNote> Update(UpdateModeratedProfileNote updateNote)
    {
        await _updateValidator.ValidateAndThrowAsync(updateNote);
        var note = await GetExistingNote(updateNote.Id);

        _intentionManager.ThrowIfForbidden(ModerationIntention.EditModNote);
        ThrowIfNotOwn(note, "Редактировать можно только свои заметки");

        var entity = new UpdateModeratedProfileNoteEntity
        {
            Id = updateNote.Id,
            Text = updateNote.Text,
            ModifiedUtc = _dateTimeProvider.Now
        };

        await _noteRepository.Update(entity);

        return (await _noteRepository.GetNote(updateNote.Id))!;
    }

    /// <inheritdoc />
    public async Task Delete(Guid noteId)
    {
        var note = await GetExistingNote(noteId);

        _intentionManager.ThrowIfForbidden(ModerationIntention.DeleteModNote);
        ThrowIfNotOwn(note, "Удалять можно только свои заметки");

        await _noteRepository.Delete(noteId);
    }

    /// <summary>
    /// The note, refused as not found when it or the user it is about is gone.
    /// </summary>
    private async Task<ModeratedProfileNote> GetExistingNote(Guid noteId)
    {
        var note = await _noteRepository.GetNote(noteId)
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ModerationNoteNotFound(noteId));

        // The call is the check: GetAsync answers 404 for a user who is no longer there.
        await _userLookupService.GetAsync(note.User.UserId);

        return note;
    }

    /// <summary>
    /// A note is changed by its author, or by a senior moderator.
    /// </summary>
    private void ThrowIfNotOwn(ModeratedProfileNote note, string refusal)
    {
        var user = _identityProvider.Current.User;
        if (note.Author.UserId != user.UserId && user.Role < UserRole.SeniorModerator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, refusal);
        }
    }
}
