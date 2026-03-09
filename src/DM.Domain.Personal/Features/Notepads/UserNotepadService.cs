using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Notepads;

namespace DM.Domain.Personal.Features.Notepads;

/// <inheritdoc />
internal class UserNotepadService : IUserNotepadService
{
    private readonly INotepadRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserNotepadService(
        INotepadRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    private Guid UserId => _identityProvider.Current.User.UserId;

    #region Entries

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntry>> GetEntries(CancellationToken ct = default)
    {
        return await _repository.GetEntries(NotepadType.User, UserId, null, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntry(entryId, ct);
        if (entry == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Entry not found");
        }

        ThrowIfNotAuthorized(entry);
        return entry;
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> CreateEntry(CreateNotepadEntry createEntry, CancellationToken ct = default)
    {
        var internalDto = new CreateNotepadEntryInternal
        {
            EntryId = _guidFactory.Create(),
            NotepadType = NotepadType.User,
            ContainerId = UserId,
            OwnerId = null,
            AuthorId = UserId,
            CategoryId = createEntry.CategoryId,
            Title = createEntry.Title,
            Content = createEntry.Content,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.CreateEntry(internalDto, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntry(entryId, ct);
        if (entry == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Entry not found");
        }

        ThrowIfNotAuthorized(entry);

        var internalDto = new UpdateNotepadEntryInternal
        {
            EntryId = entryId,
            Title = updateEntry.Title,
            Content = updateEntry.Content,
            CategoryId = updateEntry.CategoryId,
            SortOrder = updateEntry.SortOrder,
            UpdatedUtc = _dateTimeProvider.Now
        };

        return await _repository.UpdateEntry(internalDto, ct);
    }

    /// <inheritdoc />
    public async Task DeleteEntry(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntry(entryId, ct);
        if (entry == null)
        {
            return; // Already deleted
        }

        ThrowIfNotAuthorized(entry);
        await _repository.DeleteEntry(entryId, UserId, ct);
    }

    #endregion

    #region Categories

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadCategory>> GetCategories(CancellationToken ct = default)
    {
        return await _repository.GetCategories(NotepadType.User, UserId, null, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadCategory> CreateCategory(CreateNotepadCategory createCategory, CancellationToken ct = default)
    {
        var internalDto = new CreateNotepadCategoryInternal
        {
            CategoryId = _guidFactory.Create(),
            NotepadType = NotepadType.User,
            ContainerId = UserId,
            OwnerId = null,
            AuthorId = UserId,
            Name = createCategory.Name,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.CreateCategory(internalDto, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadCategory> UpdateCategory(Guid categoryId, UpdateNotepadCategory updateCategory, CancellationToken ct = default)
    {
        var category = await _repository.GetCategory(categoryId, ct);
        if (category == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Category not found");
        }

        ThrowIfNotAuthorized(category);

        var internalDto = new UpdateNotepadCategoryInternal
        {
            CategoryId = categoryId,
            Name = updateCategory.Name,
            SortOrder = updateCategory.SortOrder
        };

        return await _repository.UpdateCategory(internalDto, ct);
    }

    /// <inheritdoc />
    public async Task DeleteCategory(Guid categoryId, CancellationToken ct = default)
    {
        var category = await _repository.GetCategory(categoryId, ct);
        if (category == null)
        {
            return; // Already deleted
        }

        ThrowIfNotAuthorized(category);
        await _repository.DeleteCategory(categoryId, UserId, ct);
    }

    #endregion

    #region Authorization

    private void ThrowIfNotAuthorized(NotepadEntry entry)
    {
        if (entry.NotepadType != NotepadType.User || entry.ContainerId != UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Access denied");
        }
    }

    private void ThrowIfNotAuthorized(NotepadCategory category)
    {
        if (category.NotepadType != NotepadType.User || category.ContainerId != UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Access denied");
        }
    }

    #endregion
}
