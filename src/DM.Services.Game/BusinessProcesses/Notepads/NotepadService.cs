using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Notepads;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Characters.Reading;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.Dto;

namespace DM.Services.Game.BusinessProcesses.Notepads;

/// <inheritdoc />
internal class NotepadService : INotepadService
{
    private readonly INotepadRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameReadingService _gameReadingService;
    private readonly ICharacterReadingService _characterReadingService;

    /// <inheritdoc />
    public NotepadService(
        INotepadRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IIntentionManager intentionManager,
        IGameReadingService gameReadingService,
        ICharacterReadingService characterReadingService)
    {
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _intentionManager = intentionManager;
        _gameReadingService = gameReadingService;
        _characterReadingService = characterReadingService;
    }

    #region Entries

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntryDto>> GetGameMasterEntries(Guid gameId, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, NotepadType.Master, gameId, ct: ct);
        var entries = await _repository.GetEntries(NotepadType.Master, gameId, null, ct);
        return entries.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntryDto>> GetPlayerEntries(Guid gameId, Guid characterId, CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, NotepadType.Player, gameId, characterId, ct: ct);
        var entries = await _repository.GetEntries(NotepadType.Player, gameId, characterId, ct);
        return entries.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntryDto>> GetUserEntries(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var entries = await _repository.GetEntries(NotepadType.User, userId, null, ct);
        return entries.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<NotepadEntryDto> GetEntry(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntry(entryId, ct);
        if (entry == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Entry not found");
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, entry.NotepadType, entry.ContainerId, entry.OwnerId, entry.AuthorId, ct);
        return MapToDto(entry);
    }

    /// <inheritdoc />
    public async Task<NotepadEntryDto> CreateEntry(CreateNotepadEntry createEntry, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        // For User notepad, containerId must be current user
        if (createEntry.NotepadType == NotepadType.User)
        {
            createEntry.ContainerId = userId;
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Create, createEntry.NotepadType, createEntry.ContainerId, createEntry.OwnerId, ct: ct);

        var entry = new NotepadEntry
        {
            EntryId = _guidFactory.Create(),
            NotepadType = createEntry.NotepadType,
            ContainerId = createEntry.ContainerId,
            OwnerId = createEntry.OwnerId,
            AuthorId = userId,
            CategoryId = createEntry.CategoryId,
            Title = createEntry.Title,
            Content = createEntry.Content,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now,
            IsRemoved = false
        };

        var created = await _repository.CreateEntry(entry, ct);
        return MapToDto(created);
    }

    /// <inheritdoc />
    public async Task<NotepadEntryDto> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntry(entryId, ct);
        if (entry == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Entry not found");
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Edit, entry.NotepadType, entry.ContainerId, entry.OwnerId, entry.AuthorId, ct);

        if (updateEntry.Title != null)
            entry.Title = updateEntry.Title;
        if (updateEntry.Content != null)
            entry.Content = updateEntry.Content;
        if (updateEntry.CategoryId.HasValue)
            entry.CategoryId = updateEntry.CategoryId;
        if (updateEntry.SortOrder.HasValue)
            entry.SortOrder = updateEntry.SortOrder.Value;

        var updated = await _repository.UpdateEntry(entry, ct);
        return MapToDto(updated);
    }

    /// <inheritdoc />
    public async Task DeleteEntry(Guid entryId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var entry = await _repository.GetEntry(entryId, ct);
        if (entry == null)
        {
            return; // Already deleted
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Delete, entry.NotepadType, entry.ContainerId, entry.OwnerId, entry.AuthorId, ct);
        await _repository.DeleteEntry(entryId, userId, ct);
    }

    #endregion

    #region Categories

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadCategoryDto>> GetCategories(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        CancellationToken ct = default)
    {
        await ThrowIfNotAuthorizedAsync(NotepadIntention.Read, notepadType, containerId, ownerId, ct: ct);
        var categories = await _repository.GetCategories(notepadType, containerId, ownerId, ct);
        return categories.Select(MapCategoryToDto);
    }

    /// <inheritdoc />
    public async Task<NotepadCategoryDto> CreateCategory(CreateNotepadCategory createCategory, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        if (createCategory.NotepadType == NotepadType.User)
        {
            createCategory.ContainerId = userId;
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Create, createCategory.NotepadType, createCategory.ContainerId, createCategory.OwnerId, ct: ct);

        var category = new NotepadCategory
        {
            CategoryId = _guidFactory.Create(),
            NotepadType = createCategory.NotepadType,
            ContainerId = createCategory.ContainerId,
            OwnerId = createCategory.OwnerId,
            AuthorId = userId,
            Name = createCategory.Name,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now,
            IsRemoved = false
        };

        var created = await _repository.CreateCategory(category, ct);
        return MapCategoryToDto(created);
    }

    /// <inheritdoc />
    public async Task<NotepadCategoryDto> UpdateCategory(Guid categoryId, UpdateNotepadCategory updateCategory, CancellationToken ct = default)
    {
        var category = await _repository.GetCategory(categoryId, ct);
        if (category == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Category not found");
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Edit, category.NotepadType, category.ContainerId, category.OwnerId, category.AuthorId, ct);

        if (updateCategory.Name != null)
            category.Name = updateCategory.Name;
        if (updateCategory.SortOrder.HasValue)
            category.SortOrder = updateCategory.SortOrder.Value;

        var updated = await _repository.UpdateCategory(category, ct);
        return MapCategoryToDto(updated);
    }

    /// <inheritdoc />
    public async Task DeleteCategory(Guid categoryId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var category = await _repository.GetCategory(categoryId, ct);
        if (category == null)
        {
            return; // Already deleted
        }

        await ThrowIfNotAuthorizedAsync(NotepadIntention.Delete, category.NotepadType, category.ContainerId, category.OwnerId, category.AuthorId, ct);
        await _repository.DeleteCategory(categoryId, userId, ct);
    }

    #endregion

    #region Authorization

    private async Task<NotepadAuthContext> BuildAuthContextAsync(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        Guid? authorId = null,
        CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var context = new NotepadAuthContext
        {
            NotepadType = notepadType,
            ContainerId = containerId,
            OwnerId = ownerId,
            AuthorId = authorId,
            GameParticipation = GameParticipation.None
        };

        switch (notepadType)
        {
            case NotepadType.User:
                // No additional context needed - just check containerId == userId
                break;

            case NotepadType.Master:
                // Get game participation
                var gameMaster = await _gameReadingService.GetGame(containerId);
                context.GameParticipation = gameMaster.Participation(userId);
                break;

            case NotepadType.Player:
                // Get game participation and character owner
                var gamePlayer = await _gameReadingService.GetGame(containerId);
                context.GameParticipation = gamePlayer.Participation(userId);
                if (ownerId.HasValue)
                {
                    var character = await _characterReadingService.GetCharacter(ownerId.Value);
                    context.CharacterOwnerId = character?.Author?.UserId;
                }
                break;

            case NotepadType.Blog:
                // For blog notepad, we'd need blog participation info
                // For now, mark as authority if user is blog owner (simplified)
                // TODO: Inject IBlogReadingService when blog module is available
                break;
        }

        return context;
    }

    private async Task ThrowIfNotAuthorizedAsync(
        NotepadIntention intention,
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        Guid? authorId = null,
        CancellationToken ct = default)
    {
        var context = await BuildAuthContextAsync(notepadType, containerId, ownerId, authorId, ct);
        _intentionManager.ThrowIfForbidden(intention, context);
    }

    #endregion

    #region Mapping

    private static NotepadEntryDto MapToDto(NotepadEntry entry) => new()
    {
        Id = entry.EntryId,
        NotepadType = entry.NotepadType,
        ContainerId = entry.ContainerId,
        OwnerId = entry.OwnerId,
        CategoryId = entry.CategoryId,
        Title = entry.Title,
        Content = entry.Content,
        SortOrder = entry.SortOrder,
        CreatedUtc = entry.CreatedUtc,
        UpdatedUtc = entry.UpdatedUtc
    };

    private static NotepadCategoryDto MapCategoryToDto(NotepadCategory category) => new()
    {
        Id = category.CategoryId,
        NotepadType = category.NotepadType,
        ContainerId = category.ContainerId,
        OwnerId = category.OwnerId,
        Name = category.Name,
        SortOrder = category.SortOrder,
        CreatedUtc = category.CreatedUtc
    };

    #endregion
}
