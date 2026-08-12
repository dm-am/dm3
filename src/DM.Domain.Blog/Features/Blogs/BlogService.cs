using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Content;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Statuses;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Domain.Blog.Features.Subscriptions;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Blog.Features.Blogs;

/// <inheritdoc />
internal class BlogService : IBlogService
{
    private readonly IBlogRepository _repository;
    private readonly IBlogBlacklistRepository _blacklistRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IBlogSubscriptionService _subscriptionService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IValidator<CreateBlog> _createBlogValidator;
    private readonly IValidator<UpdateBlog> _updateBlogValidator;
    private readonly IValidator<CreateRubric> _createRubricValidator;
    private readonly IValidator<UpdateRubric> _updateRubricValidator;
    private readonly IValidator<CreatePublication> _createPublicationValidator;
    private readonly IValidator<UpdatePublication> _updatePublicationValidator;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;

    /// <inheritdoc />
    public BlogService(
        IBlogRepository repository,
        IBlogBlacklistRepository blacklistRepository,
        IUserLookupService userLookupService,
        IBlogSubscriptionService subscriptionService,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IValidator<CreateBlog> createBlogValidator,
        IValidator<UpdateBlog> updateBlogValidator,
        IValidator<CreateRubric> createRubricValidator,
        IValidator<UpdateRubric> updateRubricValidator,
        IValidator<CreatePublication> createPublicationValidator,
        IValidator<UpdatePublication> updatePublicationValidator,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IEventProducer eventProducer)
    {
        _repository = repository;
        _blacklistRepository = blacklistRepository;
        _userLookupService = userLookupService;
        _subscriptionService = subscriptionService;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _createBlogValidator = createBlogValidator;
        _updateBlogValidator = updateBlogValidator;
        _createRubricValidator = createRubricValidator;
        _updateRubricValidator = updateRubricValidator;
        _createPublicationValidator = createPublicationValidator;
        _updatePublicationValidator = updatePublicationValidator;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Blog> blogs, PagingResult paging)> GetPublicBlogs(
        PagingQuery query, BlogFilter filter, CancellationToken ct = default)
    {
        // Resolve usernames to user IDs if provided. A name nobody answers to is
        // a filter that matches nothing, not a broken request: asked through
        // GetAsync, one mistyped name in the query string answered the whole
        // listing with 410 Gone. FindUserIdAsync is the form that says "not
        // found" instead of throwing it.
        IReadOnlyCollection<Guid>? hostUserIds = null;
        if (filter.HostUsernames?.Count > 0)
        {
            var userIds = new List<Guid>();
            foreach (var username in filter.HostUsernames)
            {
                var (found, userId) = await _userLookupService.FindUserIdAsync(username, ct);
                if (found)
                {
                    userIds.Add(userId);
                }
            }

            // Every name unknown means every name filtered out. Falling back to
            // null here would drop the filter and answer with all the blogs on
            // the site, which is the opposite of what was asked.
            hostUserIds = userIds;
        }

        // The premoderation filter is a mentor review-queue tool; silently
        // ignore it for regular callers instead of failing the request. Who
        // counts as one is answered by the intention the premoderation
        // transitions ask, so the threshold stays in the resolver alone.
        var identity = _identityProvider.Current;
        var resolvedFilter = filter with
        {
            HostUserIds = hostUserIds,
            PremoderationStatuses = _intentionManager.IsAllowed(BlogIntention.SetStatusModeration)
                ? filter.PremoderationStatuses
                : null,
            CurrentUserId = identity.User.UserId
        };

        var totalCount = await _repository.CountPublicBlogs(resolvedFilter, ct);
        var pagingData = new PagingData(query, identity.Settings.Paging.EntitiesPerPage, totalCount);
        var blogs = (await _repository.GetPublicBlogs(pagingData, resolvedFilter, ct)).ToArray();

        await FillBlogUnreadCounters(blogs);
        return (blogs, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Blog>> GetPopularBlogs(
        int count = 5, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default)
    {
        var blogs = (await _repository.GetPopularBlogs(
            count, _identityProvider.Current.User.UserId, excludeOwnerIds, ct)).ToArray();
        await FillBlogUnreadCounters(blogs);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Blog>> GetUserBlogs(string username, CancellationToken ct = default)
    {
        // Unknown name throws out of the lookup; the branch that used to test for
        // null below it could never run, and reading it suggested a second answer
        // to the same case that does not exist.
        var user = await _userLookupService.GetAsync(username);

        // Premoderation-pending blogs are hidden from other viewers just like
        // games; the owner, assistants, the curator, and senior moderation
        // still see them in the profile list
        var blogs = (await _repository.GetUserBlogs(user.UserId, _identityProvider.Current.User.UserId, ct))
            .Where(b => b.PremoderationStatus == PremoderationStatus.Approved ||
                        _intentionManager.IsAllowed(BlogIntention.ViewPremoderationPending, b))
            .ToArray();
        await FillBlogUnreadCounters(blogs);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<Blog> GetAsync(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _repository.Get(blogId, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        if (blog.DraftVisibility == DraftVisibility.Private)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.ViewDraft, blog);
        }

        ThrowIfHiddenByPremoderation(blog);

        await FillBlogUnreadCounters(new[] { blog });
        await FillBlogRubricCounters(blog);
        return blog;
    }

    /// <inheritdoc />
    public async Task<Blog> GetByPublicIdAsync(string publicId, CancellationToken ct = default)
    {
        var blog = await _repository.GetByPublicId(publicId, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        if (blog.DraftVisibility == DraftVisibility.Private)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.ViewDraft, blog);
        }

        ThrowIfHiddenByPremoderation(blog);

        await FillBlogUnreadCounters(new[] { blog });
        await FillBlogRubricCounters(blog);
        return blog;
    }

    /// <inheritdoc />
    public async Task<Blog> GetByOwnerUsernameAsync(string username, CancellationToken ct = default)
    {
        var blog = await _repository.GetByOwnerUsernameAsync(username, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"Блог пользователя {username} не найден");
        }

        if (blog.DraftVisibility == DraftVisibility.Private)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.ViewDraft, blog);
        }

        ThrowIfHiddenByPremoderation(blog);

        await FillBlogUnreadCounters(new[] { blog });
        await FillBlogRubricCounters(blog);
        return blog;
    }

    /// <inheritdoc />
    public async Task<Blog> GetBlogAsync(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _repository.Get(blogId, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task AddAssistant(Guid blogId, Guid userId, CancellationToken ct = default)
    {
        // Check if already an assistant
        if (await _repository.IsAssistant(blogId, userId, ct))
        {
            return; // Already an assistant, nothing to do
        }

        var entity = new AddBlogAssistantEntity
        {
            BlogId = blogId,
            UserId = userId,
            JoinedUtc = _dateTimeProvider.Now
        };

        await _repository.AddAssistant(entity, ct);
    }

    /// <inheritdoc />
    public async Task<Blog> Create(CreateBlog createBlog, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(BlogIntention.Create);
        await _createBlogValidator.ValidateAndThrowAsync(createBlog, ct);

        var user = _identityProvider.Current.User;
        var userId = user.UserId;
        var entity = new CreateBlogEntity
        {
            BlogId = _guidFactory.Create(),
            OwnerId = userId,
            Title = createBlog.Title,
            // Blog descriptions render on the Comment surface where [mod] is a
            // green mod block; strip it when authored by a non-moderator.
            Description = ModBlockSanitizer.SanitizeForAuthor(createBlog.Description, user.Role),
            DraftVisibility = createBlog.DraftVisibility,
            CommentsEnabled = createBlog.CommentsEnabled,
            CreatedUtc = _dateTimeProvider.Now
        };
        var createdBlog = await _repository.CreateBlog(entity, ct);

        // Copy personal blacklist to blog blacklist if requested
        if (createBlog.CopyBlacklist)
        {
            await _blacklistRepository.CopyFromPersonalBlacklist(createdBlog.Id, userId, ct);
        }

        // Surface the creation as a system event. The author-subscription
        // notification generator listens for this and decides — based on
        // DraftVisibility — whether to fan out a "new blog" notification.
        await _eventProducer.SendAsync(EventType.NewBlog, createdBlog.Id);

        return createdBlog;
    }

    /// <inheritdoc />
    public async Task<Blog> Update(UpdateBlog updateBlog, CancellationToken ct = default)
    {
        await _updateBlogValidator.ValidateAndThrowAsync(updateBlog, ct);

        var blog = await GetAsync(updateBlog.BlogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        var entity = new UpdateBlogEntity
        {
            BlogId = updateBlog.BlogId,
            Title = updateBlog.Title,
            // Blog descriptions render on the Comment surface where [mod] is a
            // green mod block; strip it when the editor is a non-moderator.
            Description = ModBlockSanitizer.SanitizeForAuthor(
                updateBlog.Description, _identityProvider.Current.User.Role),
            DraftVisibility = updateBlog.DraftVisibility,
            CommentsEnabled = updateBlog.CommentsEnabled,
            UpdatedUtc = _dateTimeProvider.Now
        };
        return await _repository.UpdateBlog(entity, ct);
    }

    /// <inheritdoc />
    public async Task Delete(Guid blogId, CancellationToken ct = default)
    {
        var blog = await GetAsync(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Delete, blog);

        var userId = _identityProvider.Current.User.UserId;
        await _repository.DeleteBlog(blogId, userId, ct);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Publication> publications, PagingResult paging)> GetPublications(
        Guid blogId, Guid? rubricId, PagingQuery query, CancellationToken ct = default)
    {
        var blog = await GetAsync(blogId, ct);
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
        if (publication == null)
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
        if (publication != null)
        {
            await FillPublicationUnreadCounters(new[] { publication });
        }
        return publication;
    }

    /// <inheritdoc />
    public async Task<Publication> CreatePublication(CreatePublication createPublication, CancellationToken ct = default)
    {
        await _createPublicationValidator.ValidateAndThrowAsync(createPublication, ct);

        var blog = await GetAsync(createPublication.BlogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreatePublication, blog);

        var user = _identityProvider.Current.User;
        var userId = user.UserId;
        var now = _dateTimeProvider.Now;
        var entity = new CreatePublicationEntity
        {
            PublicationId = _guidFactory.Create(),
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
        var createdPublication = await _repository.CreatePublication(entity, ct);
        await Task.WhenAll(
            _unreadCountersRepository.CreateAsync(createdPublication.Id, createPublication.BlogId, UnreadEntryType.Message),
            _eventProducer.SendAsync(EventType.NewPublication, createdPublication.Id));
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
        await Task.WhenAll(
            _unreadCountersRepository.DeleteAsync(publicationId, UnreadEntryType.Message),
            _eventProducer.SendAsync(EventType.DeletedPublication, publicationId));
    }

    /// <inheritdoc />
    public async Task<Rubric> CreateRubric(CreateRubric createRubric, CancellationToken ct = default)
    {
        await _createRubricValidator.ValidateAndThrowAsync(createRubric, ct);

        var blog = await GetAsync(createRubric.BlogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreateRubric, blog);

        var entity = new CreateRubricEntity
        {
            RubricId = _guidFactory.Create(),
            BlogId = createRubric.BlogId,
            Title = createRubric.Title,
            SortOrder = createRubric.SortOrder
        };
        return await _repository.CreateRubric(entity, ct);
    }

    /// <inheritdoc />
    public async Task<Rubric> UpdateRubric(UpdateRubric updateRubric, CancellationToken ct = default)
    {
        await _updateRubricValidator.ValidateAndThrowAsync(updateRubric, ct);

        var (rubric, blogId) = await _repository.GetRubric(updateRubric.RubricId, ct);
        if (rubric == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.RubricNotFound);
        }

        var blog = await GetAsync(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreateRubric, blog);

        var entity = new UpdateRubricEntity
        {
            RubricId = updateRubric.RubricId,
            Title = updateRubric.Title,
            SortOrder = updateRubric.SortOrder
        };
        return await _repository.UpdateRubric(entity, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Rubric>> ReorderRubrics(
        Guid blogId, IReadOnlyList<Guid> orderedRubricIds, CancellationToken ct = default)
    {
        var blog = await GetAsync(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreateRubric, blog);

        await _repository.ReorderRubrics(blogId, orderedRubricIds, ct);

        var rubrics = (await _repository.GetRubrics(blogId, ct)).ToArray();
        await FillRubricCounters(blogId, rubrics);
        return rubrics;
    }

    /// <inheritdoc />
    public async Task DeleteRubric(Guid rubricId, CancellationToken ct = default)
    {
        var (rubric, blogId) = await _repository.GetRubric(rubricId, ct);
        if (rubric == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.RubricNotFound);
        }

        var blog = await GetAsync(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreateRubric, blog);

        var userId = _identityProvider.Current.User.UserId;
        await _repository.DeleteRubric(rubricId, userId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Rubric>> GetRubrics(Guid blogId, CancellationToken ct = default)
    {
        // Verify blog exists and user has access
        await GetAsync(blogId, ct);
        var rubrics = (await _repository.GetRubrics(blogId, ct)).ToArray();
        await FillRubricCounters(blogId, rubrics);
        return rubrics;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Subscribe(Guid blogId, CancellationToken ct = default)
    {
        var blog = await GetBlogAsync(blogId, ct);
        var userId = _identityProvider.Current.User.UserId;

        // Cannot subscribe to own blog
        if (blog.Author.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя подписаться на собственный блог");
        }

        // Check draft visibility (drafts with private visibility require invitation)
        if (blog.DraftVisibility == DraftVisibility.Private)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Этот блог доступен только по приглашению");
        }

        // Use BlogSubscriptionService for readers
        await _subscriptionService.SubscribeAsync(blogId, ct);
        return _identityProvider.Current.User;
    }

    /// <inheritdoc />
    public async Task Unsubscribe(Guid blogId, CancellationToken ct = default)
    {
        var blog = await GetBlogAsync(blogId, ct);
        var userId = _identityProvider.Current.User.UserId;

        // Owner cannot unsubscribe from their own blog
        if (blog.Author.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя отписаться от собственного блога");
        }

        // Use BlogSubscriptionService for readers
        await _subscriptionService.UnsubscribeAsync(blogId, ct);
    }

    /// <inheritdoc />
    public async Task Leave(Guid blogId, CancellationToken ct = default)
    {
        var blog = await GetBlogAsync(blogId, ct);
        var userId = _identityProvider.Current.User.UserId;

        // Owner cannot leave their own blog
        if (blog.Author.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя покинуть собственный блог");
        }

        // Remove as assistant
        var removed = await _repository.RemoveAssistant(blogId, userId, ct);
        if (!removed)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Вы не ассистент в этом блоге");
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserReference>> GetReaders(Guid blogId, CancellationToken ct = default)
    {
        await GetAsync(blogId, ct);
        // Get subscribers via BlogSubscriptionService
        return await _subscriptionService.GetReadersAsync(blogId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogUser>> GetAssistants(Guid blogId, CancellationToken ct = default)
    {
        await GetAsync(blogId, ct);
        return await _repository.GetAssistantsWithJoinDate(blogId, ct);
    }

    /// <inheritdoc />
    public async Task RemoveAssistant(Guid blogId, string username, CancellationToken ct = default)
    {
        var blog = await GetBlogAsync(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        var removed = await _repository.RemoveAssistantByUsername(blogId, username, ct);
        if (!removed)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"Ассистент {username} в этом блоге не найден");
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Blog>> GetSubscribedBlogs(IEnumerable<Guid> blogIds, CancellationToken ct = default)
    {
        var blogs = (await _repository.GetByIds(blogIds, _identityProvider.Current.User.UserId, ct)).ToArray();
        await FillBlogUnreadCounters(blogs);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Blog>> GetOwnBlogsAsync(CancellationToken ct = default)
    {
        var identity = _identityProvider.Current;
        if (!identity.User.IsAuthenticated)
        {
            return [];
        }

        var blogs = (await _repository.GetOwnBlogs(identity.User.UserId, ct)).ToArray();
        await FillBlogUnreadCounters(blogs);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<Blog> ChangePremoderationAsync(
        string id, ModulePremoderationTransition transition, CancellationToken ct = default)
    {
        // Site-wide Mentor+ gate (parameterless intention). The per-blog read
        // path hides premoderation-pending blogs from non-curators, so resolve
        // the id and fetch via the repository directly rather than the
        // read-gated GetAsync / GetByPublicIdAsync — otherwise the very blog
        // this endpoint exists to moderate would be hidden from the mentor.
        _intentionManager.ThrowIfForbidden(BlogIntention.SetStatusModeration);

        var blog = Guid.TryParse(id, out var guid)
            ? await _repository.Get(guid, ct)
            : await _repository.GetByPublicId(id, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        var blogId = blog.Id;
        var currentUserId = _identityProvider.Current.User.UserId;
        var change = ModulePremoderationPolicy.Resolve(
            transition, blog.PremoderationStatus, currentUserId);

        var update = new UpdateBlogEntity
        {
            BlogId = blogId,
            UpdatedUtc = _dateTimeProvider.Now,
            PremoderationStatus = change.Status,
            MentorId = change.MentorId,
            SetMentorId = true
        };

        var result = await _repository.UpdateBlog(update, ct);
        await _eventProducer.SendAsync(
            new List<EventType> { EventType.ChangedBlog, EventType.StatusBlogModeration }, blogId);
        return result;
    }

    /// <inheritdoc />
    public async Task<Blog> ChangeStatusAsync(
        string id, ModuleStatusTransition transition, CancellationToken ct = default)
    {
        // The endpoint is authentication-gated; resolve the id via the ungated
        // repository lookup (mirroring ChangePremoderationAsync) so the owner
        // of a premoderation-pending or private-draft blog can still operate
        // on it. The per-transition intention checks below produce the 403,
        // and legality is checked BEFORE authorization so an illegal move on
        // an accessible blog is a clean 400.
        var blog = Guid.TryParse(id, out var guid)
            ? await _repository.Get(guid, ct)
            : await _repository.GetByPublicId(id, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        var blogId = blog.Id;
        var now = _dateTimeProvider.Now;

        // What only a blog answers for: the intention that guards the move and
        // the event it publishes. Which state the move is legal from and what
        // state it produces is the machine a game runs on too, and that lives
        // in ModuleStatusPolicy.
        var (intention, statusEvent) = transition switch
        {
            ModuleStatusTransition.Start =>
                (BlogIntention.SetStatusActive, EventType.StatusBlogActive),
            ModuleStatusTransition.Freeze =>
                (BlogIntention.SetStatusClosed, EventType.StatusBlogFrozen),
            ModuleStatusTransition.Finish =>
                (BlogIntention.SetStatusClosed, EventType.StatusBlogFinished),
            ModuleStatusTransition.Close =>
                (BlogIntention.SetStatusClosed, EventType.StatusBlogClosed),
            ModuleStatusTransition.Reopen =>
                (BlogIntention.SetStatusActive, EventType.StatusBlogActive),
            _ => throw new HttpException(
                HttpStatusCode.BadRequest, RefusalMessage.UnknownStatusTransition)
        };

        var change = ModuleStatusPolicy.Resolve(
            transition,
            new ModuleLifecycle(blog.Status, blog.ClosedReason, blog.ActivatedUtc, blog.ClosedUtc),
            now);
        _intentionManager.ThrowIfForbidden(intention, blog);

        var update = new UpdateBlogEntity
        {
            BlogId = blogId,
            UpdatedUtc = now,
            Status = change.Status,
            ClosedReason = change.ClosedReason,
            ClosedUtc = change.ClosedUtc,
            ClearClosedUtc = change.ClearClosedUtc,
            ActivatedUtc = change.ActivatedUtc
        };

        var result = await _repository.UpdateBlog(update, ct);
        await _eventProducer.SendAsync(new List<EventType> { EventType.ChangedBlog, statusEvent }, blogId);
        return result;
    }

    // ═══ PRIVATE HELPERS ═══

    /// <summary>
    /// Premoderation-pending blogs are hidden from regular readers just like
    /// games: only the owner, assistants, the assigned curator, invited users,
    /// and senior moderation can open them until they are approved.
    /// </summary>
    private void ThrowIfHiddenByPremoderation(Blog blog)
    {
        if (blog.PremoderationStatus != PremoderationStatus.Approved)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.ViewPremoderationPending, blog);
        }
    }

    private async Task FillBlogUnreadCounters(Blog[] blogs)
    {
        if (blogs.Length == 0) return;

        var identity = _identityProvider.Current;

        // Anonymous users: show total counts (they can't mark anything as read)
        if (!identity.User.IsAuthenticated)
        {
            foreach (var blog in blogs)
            {
                blog.UnreadPublicationsCount = blog.PublicationCount;
                blog.UnreadCommentsCount = blog.CommentsCount;
            }
            return;
        }

        // Authenticated users: show actual unread counts
        var userId = identity.User.UserId;

        // UnreadPublicationsCount: count publications with unread comments (parent = blogId)
        var fillPublicationsTask = _unreadCountersRepository.FillParentCounters(blogs, userId,
            b => b.Id, b => b.UnreadPublicationsCount);

        // UnreadCommentsCount: total unread comments across all publications
        var fillCommentsTask = _unreadCountersRepository.FillTotalUnreadCounters(blogs, userId,
            b => b.Id, b => b.UnreadCommentsCount);

        await Task.WhenAll(fillPublicationsTask, fillCommentsTask);
    }

    /// <summary>
    /// Fill the per-rubric "(N/A)" counter (doc 4.2.1.4): N =
    /// <see cref="Rubric.UnreadPublicationsCount"/> (publications with unread
    /// content), A = <see cref="Rubric.UnreadCommentsCount"/> (total unread
    /// comments across the rubric's publications). A rubric is not itself an
    /// unread-counter entity — its publications are — so both values are
    /// derived from the per-publication counters, mirroring how the blog's own
    /// counters work but scoped to the rubric.
    /// </summary>
    private async Task FillRubricCounters(Guid blogId, ICollection<Rubric> rubrics)
    {
        if (rubrics.Count == 0) return;

        var publicationIdsByRubric = await _repository.GetRubricPublicationIds(blogId);
        var allPublicationIds = publicationIdsByRubric.Values
            .SelectMany(ids => ids)
            .Distinct()
            .ToArray();
        if (allPublicationIds.Length == 0) return;

        // Anonymous users can't mark anything read — everything is unread. The
        // Guid.Empty template counter holds the total unread items per
        // publication, so querying with any id yields the totals for them; N is
        // set to the full publication count directly (mirrors the blog-level
        // anonymous branch).
        var isAuthenticated = _identityProvider.Current.User.IsAuthenticated;
        var userId = isAuthenticated ? _identityProvider.Current.User.UserId : Guid.Empty;

        var unreadByPublication = await _unreadCountersRepository.SelectByEntitiesAsync(
            userId, UnreadEntryType.Message, allPublicationIds);

        foreach (var rubric in rubrics)
        {
            if (!publicationIdsByRubric.TryGetValue(rubric.Id, out var ids)) continue;

            rubric.UnreadCommentsCount = ids.Sum(
                id => unreadByPublication.TryGetValue(id, out var count) ? count : 0);

            rubric.UnreadPublicationsCount = isAuthenticated
                ? ids.Count(id => unreadByPublication.TryGetValue(id, out var count) && count > 0)
                : rubric.PublicationCount;
        }
    }

    /// <summary>
    /// Fill the unread counters on a single blog's embedded rubrics. The
    /// rubric list must be materialized so mutations survive serialization.
    /// </summary>
    private async Task FillBlogRubricCounters(Blog blog)
    {
        var rubrics = blog.Rubrics as ICollection<Rubric> ?? blog.Rubrics.ToArray();
        if (rubrics.Count == 0) return;

        await FillRubricCounters(blog.Id, rubrics);
        blog.Rubrics = rubrics;
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
