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
        await EnsureBlogParticipant(blogId, ct);
        return await _repository.GetEntriesAsync(NotepadType.Blog, blogId, null, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default)
    {
        var entry = await RequireEntry(entryId, ct);

        await EnsureBlogParticipant(entry.ContainerId, ct);
        return entry;
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> CreateEntry(Guid blogId, CreateNotepadEntry createEntry, CancellationToken ct = default)
    {
        await EnsureBlogParticipant(blogId, ct);

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
        var entry = await RequireEntry(entryId, ct);

        await EnsureBlogParticipant(entry.ContainerId, ct);

        // An entry is its author's own words, so nobody rewrites it for them: a
        // lead who objects to one deletes it rather than edits it. The same rule
        // the game notepads get from their resolver, stated here by hand because
        // the blog module does not see the game module's intentions.
        if (entry.AuthorId != UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

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
        var entry = await FindEntry(entryId, ct);
        if (entry == null)
        {
            return; // Already deleted
        }

        var blogOwnerId = await EnsureBlogParticipant(entry.ContainerId, ct);

        // Deleting adds the owner of the blog, who answers for what the notepad
        // holds - the blog's counterpart of the game master. An assistant and
        // the curating mentor remove only what they wrote themselves.
        if (entry.AuthorId != UserId && blogOwnerId != UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

        await _repository.DeleteEntryAsync(entryId, UserId, ct);
    }

    /// <summary>
    /// The entry, if it is one of this module's notepads.
    /// </summary>
    /// <remarks>
    /// An entry of another module's notepad is 403 and not 404: it exists, and
    /// this is simply not the door to it. A missing entry answers null here, and
    /// what that means is the caller's to decide - deleting what is already gone
    /// is what the caller asked for, reading it is not.
    /// </remarks>
    private async Task<NotepadEntry?> FindEntry(Guid entryId, CancellationToken ct)
    {
        var entry = await _repository.GetEntryAsync(entryId, ct);
        if (entry == null)
        {
            return null;
        }

        if (entry.NotepadType != NotepadType.Blog)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

        return entry;
    }

    /// <inheritdoc cref="FindEntry" />
    private async Task<NotepadEntry> RequireEntry(Guid entryId, CancellationToken ct) =>
        await FindEntry(entryId, ct)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.NotepadEntryNotFound);

    /// <summary>
    /// Who may open this notepad at all, answered with the owner of the blog.
    /// </summary>
    /// <remarks>
    /// Access to the notepad is one question and the right to a single entry in
    /// it is another, so the check hands back the fact the second one needs.
    /// Asking the blog service again for the owner would be a second read of a
    /// blog already in hand.
    /// </remarks>
    private async Task<Guid> EnsureBlogParticipant(Guid blogId, CancellationToken ct)
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

        return blog.Author.UserId;
    }
}
