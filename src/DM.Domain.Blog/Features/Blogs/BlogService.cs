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
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _eventProducer = eventProducer;
    }

    /// <summary>
    /// The user the current read is on behalf of.
    /// </summary>
    /// <remarks>
    /// Every single-blog read passes it down: the reader role of a private-draft
    /// blog is IsViewerSubscriber, and the repository can only fill that field for
    /// somebody. Guests resolve to <see cref="Guid.Empty" />, which matches no
    /// subscriber.
    /// </remarks>
    private Guid ViewerId => _identityProvider.Current.User.UserId;

    /// <inheritdoc />
    public async Task<(IEnumerable<Blog> blogs, PagingResult paging)> GetPublicBlogs(
        PagingQuery query, BlogFilter filter, CancellationToken ct = default)
    {
        // Resolve usernames to user IDs if provided. A name nobody answers to is
        // a filter that matches nothing, not a broken request: asked through
        // GetAsync, one mistyped name in the query string answered the whole
        // listing with 404. FindUserIdAsync is the form that says "not found"
        // instead of throwing it.
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
        var blog = await _repository.Get(blogId, ViewerId, ct);

        // A blog the caller may not open answers exactly as one that is not
        // there. Refusing it with a 403 instead told a stranger that the
        // identifier resolves and that what it resolves to is a private draft or
        // a blog under premoderation — the same oracle the two status endpoints
        // were closed against, reached here by an ordinary read.
        if (blog == null || !IsVisibleToViewer(blog))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        await FillBlogUnreadCounters(new[] { blog });
        await FillBlogRubricCounters(blog);
        return blog;
    }

    /// <inheritdoc />
    public async Task<Blog> GetByPublicIdAsync(string publicId, CancellationToken ct = default)
    {
        var blog = await _repository.GetByPublicId(publicId, ViewerId, ct);

        // Same answer as an alias nobody has taken: see GetAsync. This is the
        // most exposed of the read paths — the alias is five letters, the route
        // is open to guests, and BlogApiService.ResolveId funnels every
        // blog-scoped route through it.
        if (blog == null || !IsVisibleToViewer(blog))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        await FillBlogUnreadCounters(new[] { blog });
        await FillBlogRubricCounters(blog);
        return blog;
    }

    /// <inheritdoc />
    public async Task<Blog> GetByOwnerUsernameAsync(string username, CancellationToken ct = default)
    {
        var blog = await _repository.GetByOwnerUsernameAsync(username, ViewerId, ct);

        // Same answer as a user who keeps no blog, down to the wording: see
        // GetAsync. The message names the owner because that is what the caller
        // asked for, and both branches use it, so the body cannot tell the two
        // cases apart.
        if (blog == null || !IsVisibleToViewer(blog))
        {
            throw new HttpException(HttpStatusCode.NotFound, $"Блог пользователя {username} не найден");
        }

        await FillBlogUnreadCounters(new[] { blog });
        await FillBlogRubricCounters(blog);
        return blog;
    }

    /// <inheritdoc />
    public async Task<Blog> GetBlogAsync(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _repository.Get(blogId, ViewerId, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task<bool> IsVisibleToViewerAsync(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _repository.Get(blogId, ViewerId, ct);
        return blog != null && IsVisibleToViewer(blog);
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
            // Whether the blog is premoderated is decided by who is creating it,
            // by the same rule a game is decided by. The blog side never wrote
            // this field at all, so the column took its default, which is
            // Approved, and premoderation applied to nobody.
            PremoderationStatus = ModulePremoderationPolicy.InitialStatus(user),
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
        // The settings page save: open to the blog leads (owner + assistants),
        // not just the owner. Owner-only operations stay on Edit.
        _intentionManager.ThrowIfForbidden(BlogIntention.EditSettings, blog);

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

    // ═══ RUBRICS ═══
    //
    // Rubrics get no feature folder of their own. The blog read path fills
    // their derived counters (FillBlogRubricCounters below), and every rubric
    // operation starts by reading the blog. Moving them out either hands the
    // blog a dependency on a rubric service that already depends on the blog,
    // which the container refuses to build, or leaves the counter fill behind
    // as a second copy of the same computation. PATTERNS.md states the
    // exception this is an instance of.

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

        // What this call replaces is the order of the blog as a whole, so the body
        // names every rubric of that blog once. A subset would leave the rubrics it
        // skipped holding the sort orders this call has just handed to others.
        var known = (await _repository.GetRubrics(blogId, ct)).Select(r => r.Id).ToArray();
        var named = new HashSet<Guid>(orderedRubricIds);
        if (named.Count != orderedRubricIds.Count || !named.SetEquals(known))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["rubricIds"] = "Порядок должен перечислять все рубрики блога, каждую по одному разу"
            });
        }

        await _repository.ReorderRubrics(blogId, orderedRubricIds, ct);

        // Read back rather than predicted: the response carries the sort orders
        // storage now holds.
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
        // Two moves belong to moderation and one to the owner, so there is no
        // single gate for the endpoint. The moderation pair is a rank, asked
        // before anything is read, so a stranger probing aliases is refused
        // without learning whether the alias resolves. The owner's move is a fact
        // about the blog and is asked once the blog is in hand.
        //
        // Either way the fetch goes through the repository directly rather than
        // the read-gated GetAsync / GetByPublicIdAsync: that path hides a
        // premoderation-pending blog from everyone but its own people, which
        // includes the mentor who is meant to judge it.
        var isAuthorMove = transition == ModulePremoderationTransition.SubmitForApproval;
        if (!isAuthorMove)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.SetStatusModeration);
        }

        var blog = Guid.TryParse(id, out var guid)
            ? await _repository.Get(guid, ViewerId, ct)
            : await _repository.GetByPublicId(id, ViewerId, ct);

        // Which scope the row is then read under is the difference between the
        // two kinds of move, exactly as on the game side. The verdict is passed
        // on blogs nobody outside them may open, so the judge reads past
        // visibility - and pays the mentor rank for it above. The owner's move is
        // not a rank, so it reads under the ordinary viewer scope: a blog the
        // caller cannot see anywhere on the site does not exist for them here
        // either. Without this arm the endpoint - authentication-gated, because
        // the owner is normally neither mentor nor moderator - told anybody
        // probing five-letter aliases whether the alias resolves and, through the
        // state machine below, which premoderation status it resolved to.
        if (blog == null || (isAuthorMove && !IsVisibleToViewer(blog)))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        var blogId = blog.Id;
        var currentUserId = _identityProvider.Current.User.UserId;

        // Legality first and authorization second, as in ChangeStatusAsync: a move
        // the machine does not allow is a 400 whether or not the caller could have
        // made a legal one.
        var change = ModulePremoderationPolicy.Resolve(
            transition, blog.PremoderationStatus, currentUserId);
        if (isAuthorMove)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.SubmitForApproval, blog);
        }

        var update = new UpdateBlogEntity
        {
            BlogId = blogId,
            UpdatedUtc = _dateTimeProvider.Now,
            PremoderationStatus = change.Status,
            MentorId = change.MentorId,
            SetMentorId = change.SetMentorId
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
        // Every move here belongs to the blog's own leads, so unlike
        // ChangePremoderationAsync there is no rank to ask before the read and the
        // whole endpoint is one kind of move. The fetch goes through the
        // repository directly rather than the read-gated GetAsync /
        // GetByPublicIdAsync: those refuse a blog the caller may not open with a
        // 403, and 403 on this endpoint is exactly the leak.
        var blog = Guid.TryParse(id, out var guid)
            ? await _repository.Get(guid, ViewerId, ct)
            : await _repository.GetByPublicId(id, ViewerId, ct);

        // The row is then read under the ordinary viewer scope, as the owner's
        // move in ChangePremoderationAsync is: a blog the caller cannot see
        // anywhere on the site does not exist for them here either. Without this
        // arm the endpoint - authentication-gated, because a lead is normally
        // neither mentor nor moderator - was an oracle over every private draft
        // and every premoderated blog: 404 for an unclaimed alias, and from the
        // state machine and the gate below a 400 or a 403 for a taken one, which
        // told a stranger both that the alias resolves and which status it sits
        // in. The game side never had it - GameService.ChangeStatusAsync reads
        // through GetDetailsAsync under the accessibility scope, so a stranger is
        // answered 404 before the machine sees the game.
        //
        // Nobody loses a move to this: both status intentions want an owner or an
        // assistant, and an owner and an assistant pass both view gates.
        if (blog == null || !IsVisibleToViewer(blog))
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

        // Legality first and authorization second, as on the game side: an
        // illegal move on a blog the caller can see is answered 400 whether or
        // not they could have made a legal one.
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
    /// Whether the reader may see the blog at all: the private-draft gate and the
    /// premoderation gate, asked rather than thrown.
    /// </summary>
    /// <remarks>
    /// Every refusal on a blog the caller cannot see is a 404 and not a 403,
    /// because the difference between "no such blog" and "not yours" is itself
    /// the leak: the alias is five letters, so a stranger can walk the space. The
    /// game side gets this for free from GameAccessibilityFilters, which drops
    /// the row inside the query; a blog is read unfiltered and decides here.
    ///
    /// The one statement of the rule. The three single-blog reads ask it, the two
    /// status transitions ask it, and everything living inside a blog asks it
    /// through <see cref="IsVisibleToViewerAsync" />. A second copy would be a
    /// second answer to who may open a blog, and the copies would drift — as they
    /// did while the reads threw a 403 and the transitions answered 404.
    ///
    /// It decides visibility only. An action the caller may not perform on a blog
    /// they can see is still refused with a 403 by the intention guarding that
    /// action, which is what a 403 is for.
    /// </remarks>
    private bool IsVisibleToViewer(Blog blog) =>
        (blog.DraftVisibility != DraftVisibility.Private ||
         _intentionManager.IsAllowed(BlogIntention.ViewDraft, blog)) &&
        (blog.PremoderationStatus == PremoderationStatus.Approved ||
         _intentionManager.IsAllowed(BlogIntention.ViewPremoderationPending, blog));

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
}
