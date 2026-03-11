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
            CategoryId = createEntry.CategoryId,
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
            CategoryId = createEntry.CategoryId,
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
            throw new HttpException(HttpStatusCode.NotFound, "Entry not found");
        }

        // Validate this is a game notepad type
        if (entry.NotepadType != NotepadType.Master && entry.NotepadType != NotepadType.Player)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Access denied");
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, entry.NotepadType, entry.ContainerId, entry.OwnerId, ct);
        return entry;
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntryAsync(entryId, ct);
        if (entry == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Entry not found");
        }

        if (entry.NotepadType != NotepadType.Master && entry.NotepadType != NotepadType.Player)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Access denied");
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Edit, entry.NotepadType, entry.ContainerId, entry.OwnerId, ct);

        var internalDto = new UpdateNotepadEntryInternal
        {
            EntryId = entryId,
            Title = updateEntry.Title,
            Content = updateEntry.Content,
            CategoryId = updateEntry.CategoryId,
            SortOrder = updateEntry.SortOrder,
            UpdatedUtc = _dateTimeProvider.Now
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

        if (entry.NotepadType != NotepadType.Master && entry.NotepadType != NotepadType.Player)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Access denied");
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Delete, entry.NotepadType, entry.ContainerId, entry.OwnerId, ct);
        await _repository.DeleteEntryAsync(entryId, UserId, ct);
    }

    #endregion

    #region Categories

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadCategory>> GetMasterCategories(Guid gameId, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, NotepadType.Master, gameId, null, ct);
        return await _repository.GetCategoriesAsync(NotepadType.Master, gameId, null, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadCategory>> GetPlayerCategories(Guid gameId, Guid characterId, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, NotepadType.Player, gameId, characterId, ct);
        return await _repository.GetCategoriesAsync(NotepadType.Player, gameId, characterId, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadCategory> CreateMasterCategory(Guid gameId, CreateNotepadCategory createCategory, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Create, NotepadType.Master, gameId, null, ct);

        var internalDto = new CreateNotepadCategoryInternal
        {
            CategoryId = _guidFactory.Create(),
            NotepadType = NotepadType.Master,
            ContainerId = gameId,
            OwnerId = null,
            AuthorId = UserId,
            Name = createCategory.Name,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.CreateCategoryAsync(internalDto, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadCategory> CreatePlayerCategory(Guid gameId, Guid characterId, CreateNotepadCategory createCategory, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Create, NotepadType.Player, gameId, characterId, ct);

        var internalDto = new CreateNotepadCategoryInternal
        {
            CategoryId = _guidFactory.Create(),
            NotepadType = NotepadType.Player,
            ContainerId = gameId,
            OwnerId = characterId,
            AuthorId = UserId,
            Name = createCategory.Name,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.CreateCategoryAsync(internalDto, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadCategory> UpdateCategory(Guid categoryId, UpdateNotepadCategory updateCategory, CancellationToken ct = default)
    {
        var category = await _repository.GetCategoryAsync(categoryId, ct);
        if (category == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Category not found");
        }

        if (category.NotepadType != NotepadType.Master && category.NotepadType != NotepadType.Player)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Access denied");
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Edit, category.NotepadType, category.ContainerId, category.OwnerId, ct);

        var internalDto = new UpdateNotepadCategoryInternal
        {
            CategoryId = categoryId,
            Name = updateCategory.Name,
            SortOrder = updateCategory.SortOrder
        };

        return await _repository.UpdateCategoryAsync(internalDto, ct);
    }

    /// <inheritdoc />
    public async Task DeleteCategory(Guid categoryId, CancellationToken ct = default)
    {
        var category = await _repository.GetCategoryAsync(categoryId, ct);
        if (category == null)
        {
            return; // Already deleted
        }

        if (category.NotepadType != NotepadType.Master && category.NotepadType != NotepadType.Player)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Access denied");
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Delete, category.NotepadType, category.ContainerId, category.OwnerId, ct);
        await _repository.DeleteCategoryAsync(categoryId, UserId, ct);
    }

    #endregion

    #region Authorization

    private async Task<NotepadAuthContext> BuildAuthContextAsync(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId,
        CancellationToken ct)
    {
        var context = new NotepadAuthContext
        {
            NotepadType = notepadType,
            ContainerId = containerId,
            OwnerId = ownerId,
            AuthorId = null,
            GameRoles = Array.Empty<GameRole>()
        };

        switch (notepadType)
        {
            case NotepadType.Master:
                var gameMaster = await _gameService.GetAsync(containerId);
                context.GameRoles = gameMaster.GetRoles(UserId);
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

    private async Task ThrowIfNotAuthorizedAsync(
        NotepadIntention intention,
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId,
        CancellationToken ct)
    {
        var context = await BuildAuthContextAsync(notepadType, containerId, ownerId, ct);
        _intentionManager.ThrowIfForbidden(intention, context);
    }

    #endregion
}
