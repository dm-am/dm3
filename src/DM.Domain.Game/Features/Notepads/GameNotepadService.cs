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
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, NotepadType.Master, gameId, null, ct);
        return await _repository.GetEntriesAsync(NotepadType.Master, gameId, null, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> CreateMasterEntry(Guid gameId, CreateNotepadEntry createEntry, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Create, NotepadType.Master, gameId, null, ct);

        var internalDto = new CreateNotepadEntryInternal
        {
            EntryId = _guidFactory.Create(),
            NotepadType = NotepadType.Master,
            ContainerId = gameId,
            OwnerId = null,
            AuthorId = UserId,
            Title = createEntry.Title,
            Content = createEntry.Content,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.CreateEntryAsync(internalDto, ct);
    }

    #endregion

    #region Player/Character Notepad

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntry>> GetPlayerEntries(Guid gameId, Guid characterId, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, NotepadType.Player, gameId, characterId, ct);
        return await _repository.GetEntriesAsync(NotepadType.Player, gameId, characterId, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> CreatePlayerEntry(Guid gameId, Guid characterId, CreateNotepadEntry createEntry, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Create, NotepadType.Player, gameId, characterId, ct);

        var internalDto = new CreateNotepadEntryInternal
        {
            EntryId = _guidFactory.Create(),
            NotepadType = NotepadType.Player,
            ContainerId = gameId,
            OwnerId = characterId,
            AuthorId = UserId,
            Title = createEntry.Title,
            Content = createEntry.Content,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.CreateEntryAsync(internalDto, ct);
    }

    #endregion

    #region Character Master Notepad

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntry>> GetCharacterMasterEntries(Guid gameId, Guid characterId, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, NotepadType.CharacterMaster, gameId, characterId, ct);
        return await _repository.GetEntriesAsync(NotepadType.CharacterMaster, gameId, characterId, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> CreateCharacterMasterEntry(Guid gameId, Guid characterId, CreateNotepadEntry createEntry, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Create, NotepadType.CharacterMaster, gameId, characterId, ct);

        var internalDto = new CreateNotepadEntryInternal
        {
            EntryId = _guidFactory.Create(),
            NotepadType = NotepadType.CharacterMaster,
            ContainerId = gameId,
            OwnerId = characterId,
            AuthorId = UserId,
            Title = createEntry.Title,
            Content = createEntry.Content,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.CreateEntryAsync(internalDto, ct);
    }

    #endregion

    #region Common Operations

    /// <inheritdoc />
    public async Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntryAsync(entryId, ct);
        if (entry == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.NotepadEntryNotFound);
        }

        ThrowIfNotAGameNotepad(entry);

        await ThrowIfNotAuthorizedForEntryAsync(NotepadIntention.Read, entry, ct);
        return entry;
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntryAsync(entryId, ct);
        if (entry == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.NotepadEntryNotFound);
        }

        ThrowIfNotAGameNotepad(entry);

        await ThrowIfNotAuthorizedForEntryAsync(NotepadIntention.Edit, entry, ct);

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
            return; // Already deleted
        }

        ThrowIfNotAGameNotepad(entry);

        await ThrowIfNotAuthorizedForEntryAsync(NotepadIntention.Delete, entry, ct);
        await _repository.DeleteEntryAsync(entryId, UserId, ct);
    }

    #endregion

    #region Authorization

    private async Task<NotepadAuthContext> BuildAuthContextAsync(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId,
        Guid? authorId,
        CancellationToken ct)
    {
        var context = new NotepadAuthContext
        {
            NotepadType = notepadType,
            ContainerId = containerId,
            OwnerId = ownerId,
            AuthorId = authorId,
            GameRoles = Array.Empty<GameRole>()
        };

        switch (notepadType)
        {
            case NotepadType.Master:
            case NotepadType.CharacterMaster:
                var gameLeads = await _gameService.GetAsync(containerId);
                context.GameRoles = gameLeads.GetRoles(UserId);
                break;
            case NotepadType.Player:
                var gamePlayer = await _gameService.GetAsync(containerId);
                context.GameRoles = gamePlayer.GetRoles(UserId);
                if (ownerId.HasValue)
                {
                    var character = await _characterService.GetAsync(ownerId.Value);
                    context.CharacterOwnerId = character?.Author?.UserId;
                }
                break;
        }

        return context;
    }

    /// <summary>
    /// The three notepads this service answers for. A notepad entry is one row
    /// in one table shared with the blog and personal notepads, and the entry
    /// endpoints take an id and nothing else - so an id belonging to somebody
    /// else's notepad has to be refused here, before an authorization context
    /// this resolver cannot build gets built out of it.
    /// </summary>
    private static void ThrowIfNotAGameNotepad(NotepadEntry entry)
    {
        if (entry.NotepadType is not (NotepadType.Master or NotepadType.Player or NotepadType.CharacterMaster))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }
    }

    /// <summary>
    /// Authorization for a question about the notepad as a whole - listing its
    /// entries or adding one. No entry exists yet, so there is no author to
    /// name, and the resolver settles both on access alone.
    /// </summary>
    private Task ThrowIfNotAuthorizedAsync(
        NotepadIntention intention,
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId,
        CancellationToken ct) =>
        ThrowIfNotAuthorizedAsync(intention, notepadType, containerId, ownerId, null, ct);

    /// <summary>
    /// Authorization for a question about one entry. Editing belongs to its
    /// author alone and deleting to the author and the game master, so the
    /// author has to reach the resolver: a context without one refuses both.
    /// </summary>
    private Task ThrowIfNotAuthorizedForEntryAsync(
        NotepadIntention intention,
        NotepadEntry entry,
        CancellationToken ct) =>
        ThrowIfNotAuthorizedAsync(
            intention, entry.NotepadType, entry.ContainerId, entry.OwnerId, entry.AuthorId, ct);

    private async Task ThrowIfNotAuthorizedAsync(
        NotepadIntention intention,
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId,
        Guid? authorId,
        CancellationToken ct)
    {
        var context = await BuildAuthContextAsync(notepadType, containerId, ownerId, authorId, ct);
        _intentionManager.ThrowIfForbidden(intention, context);
    }

    #endregion
}
