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

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntry>> GetEntries(CancellationToken ct = default)
    {
        return await _repository.GetEntriesAsync(NotepadType.User, UserId, null, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntryAsync(entryId, ct);
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
            Title = createEntry.Title,
            Content = createEntry.Content,
            SortOrder = 0,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.CreateEntryAsync(internalDto, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntryAsync(entryId, ct);
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

        ThrowIfNotAuthorized(entry);
        await _repository.DeleteEntryAsync(entryId, UserId, ct);
    }

    private void ThrowIfNotAuthorized(NotepadEntry entry)
    {
        if (entry.NotepadType != NotepadType.User || entry.ContainerId != UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Access denied");
        }
    }
}
