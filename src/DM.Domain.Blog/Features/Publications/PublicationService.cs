using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Blog.Features.Publications;

/// <inheritdoc />
/// <remarks>
/// Depends on the blog one way round: a publication is read and written inside
/// a blog, and nothing on the blog side asks a publication anything. That is
/// what makes the split possible at all — see the exception in PATTERNS.md for
/// the shape that cannot be split.
/// </remarks>
internal class PublicationService : IPublicationService
{
    private readonly IPublicationRepository _repository;
    private readonly IBlogService _blogService;
    private readonly IUserLookupService _userLookupService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IValidator<CreatePublication> _createPublicationValidator;
    private readonly IValidator<UpdatePublication> _updatePublicationValidator;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;

    /// <inheritdoc />
    public PublicationService(
        IPublicationRepository repository,
        IBlogService blogService,
        IUserLookupService userLookupService,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IValidator<CreatePublication> createPublicationValidator,
        IValidator<UpdatePublication> updatePublicationValidator,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IEventProducer eventProducer)
    {
        _repository = repository;
        _blogService = blogService;
        _userLookupService = userLookupService;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _createPublicationValidator = createPublicationValidator;
        _updatePublicationValidator = updatePublicationValidator;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Publication> publications, PagingResult paging)> GetPublications(
        Guid blogId, Guid? rubricId, PagingQuery query, CancellationToken ct = default)
    {
        var blog = await _blogService.GetAsync(blogId, ct);
        var includeUnpublished = _intentionManager.IsAllowed(BlogIntention.ViewDraft, blog);

        var totalCount = await _repository.CountPublications(blogId, rubricId, includeUnpublished, ct);
        var pagingData = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);

        var publications = (await _repository.GetPublications(blogId, rubricId, includeUnpublished, pagingData, ct)).ToArray();
        await FillPublicationUnreadCounters(publications);
        return (publications, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Publication> GetPublication(Guid publicationId, CancellationToken ct = default)
    {
        var publication = await _repository.GetPublication(publicationId, ct);

        // The row is only half the answer: a publication is read inside a blog,
        // and a blog the reader may not open hides everything in it. The listing
        // above has always known that: it reads the blog through the gated
        // GetAsync first, while this read asked the publication alone, so a
        // published publication in a private-draft blog, or in one still waiting
        // on premoderation, was handed to anybody holding its id, guest included.
        // Nothing had to be guessed: the profile widget below hands the id out.
        //
        // Absent and hidden answer with one and the same sentence on purpose. A
        // 403 here would confirm that the publication exists, which is the half
        // of the leak that closing the read alone does not close.
        if (publication == null ||
            !await _blogService.IsVisibleToViewerAsync(publication.BlogId, ct))
        {
            throw new HttpException(HttpStatusCode.NotFound, "Публикация не найдена");
        }

        if (!publication.IsPublished)
        {
            _intentionManager.ThrowIfForbidden(PublicationIntention.ViewDraft, publication);
        }

        await FillPublicationUnreadCounters(new[] { publication });
        return publication;
    }

    /// <inheritdoc />
    public async Task<Publication?> GetBestUserPublication(string username, CancellationToken ct = default)
    {
        // Resolve username → UserId via the cross-module lookup so we keep
        // the repository's parameter typed (Guid) — repositories never
        // take usernames directly. An unknown name throws HttpException(410),
        // and 410 is what the caller receives: the error middleware passes an
        // HttpException status through untouched.
        var user = await _userLookupService.GetAsync(username);

        var publication = await _repository.GetBestUserPublication(user.UserId, ct);

        // The widget names one publication and links to it, so the one it names
        // has to be one the reader could open. The repository filters removals and
        // drafts, and nothing there can know the blog underneath is a private
        // draft or is still waiting on premoderation: the rule is about the
        // reader, not about the row.
        //
        // A hidden best is no answer rather than the next best one: walking down
        // the list would cost a blog read per candidate, and an author whose blogs
        // are all hidden has nothing to show here in any case.
        if (publication == null ||
            !await _blogService.IsVisibleToViewerAsync(publication.BlogId, ct))
        {
            return null;
        }

        await FillPublicationUnreadCounters(new[] { publication });
        return publication;
    }

    /// <inheritdoc />
    public async Task<Publication> CreatePublication(CreatePublication createPublication, CancellationToken ct = default)
    {
        await _createPublicationValidator.ValidateAndThrowAsync(createPublication, ct);

        var blog = await _blogService.GetAsync(createPublication.BlogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreatePublication, blog);

        var user = _identityProvider.Current.User;
        var userId = user.UserId;
        var now = _dateTimeProvider.Now;
        var publicationId = _guidFactory.Create();
        var entity = new CreatePublicationEntity
        {
            PublicationId = publicationId,
            BlogId = createPublication.BlogId,
            RubricId = createPublication.RubricId,
            AuthorId = userId,
            Title = createPublication.Title,
            // Publication bodies render on the Comment surface where [mod] is a
            // green mod block; strip it when authored by a non-moderator.
            Content = ModBlockSanitizer.SanitizeForAuthor(createPublication.Content, user.Role),
            Preview = createPublication.Preview,
            CommentsEnabled = createPublication.CommentsEnabled,
            PublishImmediately = createPublication.PublishImmediately,
            CreatedUtc = now
        };
        // Markers first, row second, commit on the line after it returns.
        await using var counters = await _unreadCountersRepository.ReserveAsync(
            UnreadMarker.UnderParent(publicationId, createPublication.BlogId, UnreadEntryType.Message));

        var createdPublication = await _repository.CreatePublication(entity, ct);
        counters.Commit();

        await _eventProducer.SendAsync(EventType.NewPublication, createdPublication.Id);
        return createdPublication;
    }

    /// <inheritdoc />
    public async Task<Publication> UpdatePublication(UpdatePublication updatePublication, CancellationToken ct = default)
    {
        await _updatePublicationValidator.ValidateAndThrowAsync(updatePublication, ct);

        var publication = await GetPublication(updatePublication.PublicationId, ct);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Edit, publication);

        // Check if publishing for the first time
        if (updatePublication.IsPublished == true && !publication.IsPublished)
        {
            _intentionManager.ThrowIfForbidden(PublicationIntention.Publish, publication);
        }

        var entity = new UpdatePublicationEntity
        {
            PublicationId = updatePublication.PublicationId,
            RubricId = updatePublication.RubricId,
            ClearRubric = updatePublication.ClearRubric,
            Title = updatePublication.Title,
            // Publication bodies render on the Comment surface where [mod] is a
            // green mod block; strip it when the editor is a non-moderator.
            Content = ModBlockSanitizer.SanitizeForAuthor(
                updatePublication.Content, _identityProvider.Current.User.Role),
            Preview = updatePublication.Preview,
            CommentsEnabled = updatePublication.CommentsEnabled,
            IsPublished = updatePublication.IsPublished,
            UpdatedUtc = _dateTimeProvider.Now,
            ModifiedByUserId = _identityProvider.Current.User.UserId
        };
        var updatedPublication = await _repository.UpdatePublication(entity, ct);
        await _eventProducer.SendAsync(EventType.ChangedPublication, updatedPublication.Id);
        return updatedPublication;
    }

    /// <inheritdoc />
    public async Task DeletePublication(Guid publicationId, CancellationToken ct = default)
    {
        var publication = await GetPublication(publicationId, ct);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Delete, publication);

        var userId = _identityProvider.Current.User.UserId;
        await _repository.DeletePublication(publicationId, userId, ct);
        // Sequential: the counter tombstone is a DmDbContext write now, and a
        // context refuses to share the scope with a parallel operation (INV-7).
        await _unreadCountersRepository.DeleteAsync(publicationId, UnreadEntryType.Message);
        await _eventProducer.SendAsync(EventType.DeletedPublication, publicationId);
    }

    private async Task FillPublicationUnreadCounters(Publication[] publications)
    {
        if (publications.Length == 0) return;

        var identity = _identityProvider.Current;

        // Anonymous users: show total counts
        if (!identity.User.IsAuthenticated)
        {
            foreach (var publication in publications)
            {
                publication.UnreadCommentsCount = publication.CommentCount;
            }
            return;
        }

        // Authenticated users: show actual unread counts
        await _unreadCountersRepository.FillEntityCounters(publications, identity.User.UserId,
            p => p.Id, p => p.UnreadCommentsCount);
    }
}
