using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Notepads;
using DM.Domain.Blog.Features.Blogs;

namespace DM.Domain.Blog.Features.Notepads;

/// <inheritdoc />
internal class BlogNotepadService : IBlogNotepadService
{
    private readonly INotepadRepository _repository;
    private readonly IBlogService _blogService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BlogNotepadService(
        INotepadRepository repository,
        IBlogService blogService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _blogService = blogService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    private Guid UserId => _identityProvider.Current.User.UserId;

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntry>> GetEntries(Guid blogId, CancellationToken ct = default)
    {
        await ThrowIfNotBlogParticipant(blogId, ct);
        return await _repository.GetEntriesAsync(NotepadType.Blog, blogId, null, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _repository.GetEntryAsync(entryId, ct);
        if (entry == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.NotepadEntryNotFound);
        }

        if (entry.NotepadType != NotepadType.Blog)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

        await ThrowIfNotBlogParticipant(entry.ContainerId, ct);
        return entry;
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> CreateEntry(Guid blogId, CreateNotepadEntry createEntry, CancellationToken ct = default)
    {
        await ThrowIfNotBlogParticipant(blogId, ct);

        var internalDto = new CreateNotepadEntryInternal
        {
            EntryId = _guidFactory.Create(),
            NotepadType = NotepadType.Blog,
            ContainerId = blogId,
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
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.NotepadEntryNotFound);
        }

        if (entry.NotepadType != NotepadType.Blog)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

        await ThrowIfNotBlogParticipant(entry.ContainerId, ct);

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

        if (entry.NotepadType != NotepadType.Blog)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

        await ThrowIfNotBlogParticipant(entry.ContainerId, ct);
        await _repository.DeleteEntryAsync(entryId, UserId, ct);
    }

    private async Task ThrowIfNotBlogParticipant(Guid blogId, CancellationToken ct)
    {
        var blog = await _blogService.GetBlogAsync(blogId, ct);
        var isOwner = blog.Author.UserId == UserId;
        var isAssistant = blog.Assistants.Any(a => a.UserId == UserId);
        // Notepad access = owner + assistants + mentor (curator), per doc.
        var isMentor = blog.Mentor?.UserId == UserId;

        if (!isOwner && !isAssistant && !isMentor)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нет доступа к заметкам блога");
        }
    }
}
