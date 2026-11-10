using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Notepads;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Notepads;

/// <inheritdoc />
internal class GameNotepadService : IGameNotepadService
{
    private readonly INotepadRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameService _gameService;
    private readonly ICharacterService _characterService;

    public GameNotepadService(
        INotepadRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IIntentionManager intentionManager,
        IGameService gameService,
        ICharacterService characterService)
    {
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _intentionManager = intentionManager;
        _gameService = gameService;
        _characterService = characterService;
    }

    private Guid UserId => _identityProvider.Current.User.UserId;

    #region Master Notepad

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntry>> GetMasterEntries(Guid gameId, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, NotepadType.Master, gameId, null);
        return await _repository.GetEntriesAsync(NotepadType.Master, gameId, null, ct);
    }

    /// <inheritdoc />
    public Task<NotepadEntry> CreateMasterEntry(Guid gameId, CreateNotepadEntry createEntry, CancellationToken ct = default) =>
        CreateEntry(NotepadType.Master, gameId, ownerId: null, createEntry, ct);

    #endregion

    #region Player/Character Notepad

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntry>> GetPlayerEntries(Guid gameId, Guid characterId, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, NotepadType.Player, gameId, characterId);
        return await _repository.GetEntriesAsync(NotepadType.Player, gameId, characterId, ct);
    }

    /// <inheritdoc />
    public Task<NotepadEntry> CreatePlayerEntry(Guid gameId, Guid characterId, CreateNotepadEntry createEntry, CancellationToken ct = default) =>
        CreateEntry(NotepadType.Player, gameId, characterId, createEntry, ct);

    #endregion

    #region Character Master Notepad

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntry>> GetCharacterMasterEntries(Guid gameId, Guid characterId, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, NotepadType.CharacterMaster, gameId, characterId);
        return await _repository.GetEntriesAsync(NotepadType.CharacterMaster, gameId, characterId, ct);
    }

    /// <inheritdoc />
    public Task<NotepadEntry> CreateCharacterMasterEntry(Guid gameId, Guid characterId, CreateNotepadEntry createEntry, CancellationToken ct = default) =>
        CreateEntry(NotepadType.CharacterMaster, gameId, characterId, createEntry, ct);

    #endregion

    #region Common Operations

    private async Task<NotepadEntry> CreateEntry(
        NotepadType notepadType,
        Guid gameId,
        Guid? ownerId,
        CreateNotepadEntry createEntry,
        CancellationToken ct)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Create, notepadType, gameId, ownerId);

        var internalDto = new CreateNotepadEntryInternal
        {
            EntryId = _guidFactory.Create(),
            NotepadType = notepadType,
            ContainerId = gameId,
            OwnerId = ownerId,
            AuthorId = UserId,
            Title = createEntry.Title,
            Content = createEntry.Content,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.CreateEntryAsync(internalDto, ct);
    }

    /// <inheritdoc />
    public Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default) =>
        AllowedEntry(entryId, NotepadIntention.Read, ct);

    /// <inheritdoc />
    public async Task<NotepadEntry> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default)
    {
        await AllowedEntry(entryId, NotepadIntention.Edit, ct);

        var internalDto = new UpdateNotepadEntryInternal
        {
            EntryId = entryId,
            Title = updateEntry.Title,
            Content = updateEntry.Content,
            SortOrder = updateEntry.SortOrder,
            ModifiedUtc = _dateTimeProvider.Now
        };

        return await _repository.UpdateEntryAsync(internalDto, ct);
    }

    /// <inheritdoc />
    public async Task DeleteEntry(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntryAsync(entryId, ct);
        if (entry == null)
        {
            return;
        }

        ThrowIfNotAGameNotepad(entry);

        try
        {
            await ThrowIfNotAuthorizedForEntryAsync(NotepadIntention.Delete, entry);
        }
        catch (HttpException refusal) when (HiddenContainerRefusal.IsAbsence(refusal))
        {
            // An entry of a game the reader may not open is as gone as a deleted one.
            return;
        }

        await _repository.DeleteEntryAsync(entryId, UserId, ct);
    }

    // An entry of a game the reader may not open answers as a missing one.
    private async Task<NotepadEntry> AllowedEntry(Guid entryId, NotepadIntention intention, CancellationToken ct)
    {
        var entry = await _repository.GetEntryAsync(entryId, ct) ??
                    throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.NotepadEntryNotFound);

        ThrowIfNotAGameNotepad(entry);

        await HiddenContainerRefusal.AsNotFound(RefusalMessage.NotepadEntryNotFound,
            () => ThrowIfNotAuthorizedForEntryAsync(intention, entry));
        return entry;
    }

    #endregion

    #region Authorization

    private async Task<NotepadAuthContext> BuildAuthContextAsync(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId,
        Guid? authorId)
    {
        var context = new NotepadAuthContext
        {
            NotepadType = notepadType,
            AuthorId = authorId
        };

        var game = await _gameService.GetAsync(containerId);
        context.GameRoles = game.GetRoles(UserId);

        if (notepadType == NotepadType.Player && ownerId.HasValue)
        {
            var character = await _characterService.GetAsync(ownerId.Value);
            context.CharacterOwnerId = character.Author?.UserId;
        }

        return context;
    }

    // The entry table is shared with the blog and personal notepads, and the entry endpoints take
    // an id alone.
    private static void ThrowIfNotAGameNotepad(NotepadEntry entry)
    {
        if (entry.NotepadType is not (NotepadType.Master or NotepadType.Player or NotepadType.CharacterMaster))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }
    }

    // A context without the author refuses Edit and Delete.
    private Task ThrowIfNotAuthorizedForEntryAsync(
        NotepadIntention intention,
        NotepadEntry entry) =>
        ThrowIfNotAuthorizedAsync(
            intention, entry.NotepadType, entry.ContainerId, entry.OwnerId, entry.AuthorId);

    // Listing or adding has no entry yet, so no author.
    private async Task ThrowIfNotAuthorizedAsync(
        NotepadIntention intention,
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId,
        Guid? authorId = null)
    {
        var context = await BuildAuthContextAsync(notepadType, containerId, ownerId, authorId);
        _intentionManager.ThrowIfForbidden(intention, context);
    }

    #endregion
}
