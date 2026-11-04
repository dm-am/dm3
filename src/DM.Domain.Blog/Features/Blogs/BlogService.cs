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
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Content;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Statuses;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Blog.Features.Blogs;

/// <inheritdoc />
internal class BlogService : IBlogService
{
    private readonly IBlogRepository _repository;
    private readonly IBlogBlacklistRepository _blacklistRepository;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IValidator<CreateBlog> _createBlogValidator;
    private readonly IValidator<UpdateBlog> _updateBlogValidator;
    private readonly IValidator<CreateRubric> _createRubricValidator;
    private readonly IValidator<UpdateRubric> _updateRubricValidator;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;
    private readonly BlogUnreadCounters _unreadCounters;

    public BlogService(
        IBlogRepository repository,
        IBlogBlacklistRepository blacklistRepository,
        IUserBlacklistChecker userBlacklistChecker,
        IUserLookupService userLookupService,
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
        _userBlacklistChecker = userBlacklistChecker;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _createBlogValidator = createBlogValidator;
        _updateBlogValidator = updateBlogValidator;
        _createRubricValidator = createRubricValidator;
        _updateRubricValidator = updateRubricValidator;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _eventProducer = eventProducer;
        _unreadCounters = new BlogUnreadCounters(repository, unreadCountersRepository, identityProvider);
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
        // A name nobody answers to is a filter that matches nothing, not a 404.
        IReadOnlyCollection<Guid>? hostUserIds = null;
        if (filter.HostUsernames?.Count > 0)
        {
            var userIds = new List<Guid>();
            foreach (var username in filter.HostUsernames)
            {
                // "Аккаунт удален" is no account: it hosts through every removed one,
                // whose identifiers narrow the list on the server and go no further.
                if (RemovedAccount.Is(username))
                {
                    userIds.AddRange(await _userLookupService.GetRemovedAccountIdsAsync());
                    continue;
                }

                var (found, userId) = await _userLookupService.FindUserIdAsync(username, ct);
                if (found)
                {
                    userIds.Add(userId);
                }
            }

            // Every name unknown means every name filtered out, not the filter dropped.
            hostUserIds = userIds;
        }

        // A mentor review-queue tool, silently ignored for regular callers; the
        // threshold stays in the resolver.
        var identity = _identityProvider.Current;
        var premoderationStatuses = _intentionManager.IsAllowed(BlogIntention.SetStatusModeration)
            ? filter.PremoderationStatuses
            : null;

        // "Скрывать блоги" cleans the listings that offer blogs to read, not a list asked
        // for by the author's name nor the moderation queue (GameService.IsDiscoveryListing).
        var excludedOwners = identity.User.IsAuthenticated && hostUserIds is null &&
                             premoderationStatuses is not { Count: > 0 }
            ? await _userBlacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(
                identity.User.UserId, UserBlacklistSettings.HideBlogs, ct)
            : null;

        var resolvedFilter = filter with
        {
            HostUserIds = hostUserIds,
            ExcludeOwnerIds = excludedOwners is { Count: > 0 } ? excludedOwners : null,
            PremoderationStatuses = premoderationStatuses,
            CurrentUserId = identity.User.UserId
        };

        var totalCount = await _repository.CountPublicBlogs(resolvedFilter, ct);
        var pagingData = new PagingData(query, identity.Settings.PageSize, totalCount);
        var blogs = (await _repository.GetPublicBlogs(pagingData, resolvedFilter, ct)).ToArray();

        await _unreadCounters.FillBlogs(blogs);
        return (blogs, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Blog>> GetPopularBlogs(
        int count = 5, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default)
    {
        var blogs = (await _repository.GetPopularBlogs(
            count, _identityProvider.Current.User.UserId, excludeOwnerIds, ct)).ToArray();
        await _unreadCounters.FillBlogs(blogs);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Blog>> GetUserBlogs(string username, CancellationToken ct = default)
    {
        var user = await _userLookupService.GetAsync(username);

        // Premoderation-pending blogs are hidden from other viewers just like games
        var blogs = (await _repository.GetUserBlogs(user.UserId, _identityProvider.Current.User.UserId, ct))
            .Where(b => b.PremoderationStatus == PremoderationStatus.Approved ||
                        _intentionManager.IsAllowed(BlogIntention.ViewPremoderationPending, b))
            .ToArray();
        await _unreadCounters.FillBlogs(blogs);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<Blog> GetAsync(Guid blogId, CancellationToken ct = default) =>
        await FillCounters(await GetVisibleAsync(blogId, ct));

    /// <inheritdoc />
    public async Task<Blog> GetByPublicIdAsync(string publicId, CancellationToken ct = default)
    {
        var blog = await _repository.GetByPublicId(publicId, ViewerId, ct);

        // Same answer as an alias nobody has taken: the alias is five letters and the route is open to guests.
        if (blog == null || !IsVisibleToViewer(blog))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        return await FillCounters(blog);
    }

    /// <inheritdoc />
    public async Task<Blog> GetByOwnerUsernameAsync(string username, CancellationToken ct = default)
    {
        var blog = await _repository.GetByOwnerUsernameAsync(username, ViewerId, ct);

        // Same answer as a user who keeps no blog, down to the wording.
        if (blog == null || !IsVisibleToViewer(blog))
        {
            throw new HttpException(HttpStatusCode.NotFound, $"Блог пользователя {username} не найден");
        }

        return await FillCounters(blog);
    }

    /// <inheritdoc />
    public async Task<Blog> GetUnfiltered(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _repository.Get(blogId, ViewerId, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task<Blog> GetVisibleAsync(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _repository.Get(blogId, ViewerId, ct);
        if (blog == null || !IsVisibleToViewer(blog))
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
        if (await _repository.IsAssistant(blogId, userId, ct))
        {
            return;
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
            // Descriptions render on the Comment surface, where [mod] is a mod block.
            Description = ModBlockSanitizer.SanitizeForAuthor(createBlog.Description, user.Role),
            // Decided by who is creating it, by the same rule a game is decided by.
            PremoderationStatus = ModulePremoderationPolicy.InitialStatus(user),
            DraftVisibility = createBlog.DraftVisibility,
            CreatedUtc = _dateTimeProvider.Now
        };
        var createdBlog = await _repository.CreateBlog(entity, ct);

        // The request decides when it says anything; silent, the author's account
        // setting does, the same rule a new game follows (GameService.CreateAsync).
        var copyBlacklist = createBlog.CopyBlacklist ??
                            (await _userBlacklistChecker.GetSettingsAsync(userId, ct))
                            .HasFlag(UserBlacklistSettings.AutoPopulateContentBlacklist);
        if (copyBlacklist)
        {
            await _blacklistRepository.CopyFromPersonalBlacklist(createdBlog.Id, userId, ct);
        }

        // The author-subscription generator decides by DraftVisibility whether to fan out "new blog".
        await _eventProducer.SendAsync(EventType.NewBlog, createdBlog.Id);

        return createdBlog;
    }

    /// <inheritdoc />
    public async Task<Blog> Update(UpdateBlog updateBlog, CancellationToken ct = default)
    {
        await _updateBlogValidator.ValidateAndThrowAsync(updateBlog, ct);

        var blog = await GetAsync(updateBlog.BlogId, ct);
        // The settings page is open to the blog leads; owner-only operations stay on Edit.
        _intentionManager.ThrowIfForbidden(BlogIntention.EditSettings, blog);

        var entity = new UpdateBlogEntity
        {
            BlogId = updateBlog.BlogId,
            Title = updateBlog.Title,
            // Descriptions render on the Comment surface, where [mod] is a mod block.
            Description = ModBlockSanitizer.SanitizeForAuthor(
                AuthoredText.AsTyped(updateBlog.Description), _identityProvider.Current.User.Role),
            DraftVisibility = updateBlog.DraftVisibility,
            ModifiedUtc = _dateTimeProvider.Now
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

    // Rubrics live in the blog service: every rubric operation reads the blog,
    // and the blog read fills their counters (the exception PATTERNS.md states).

    /// <inheritdoc />
    public async Task<Rubric> CreateRubric(CreateRubric createRubric, CancellationToken ct = default)
    {
        await _createRubricValidator.ValidateAndThrowAsync(createRubric, ct);

        var blog = await GetAsync(createRubric.BlogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.ManageRubrics, blog);

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
        await ThrowIfRubricForbidden(updateRubric.RubricId, ct);

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
        _intentionManager.ThrowIfForbidden(BlogIntention.ManageRubrics, blog);

        // The order of the blog as a whole: a subset would leave the rubrics it
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
        await _unreadCounters.FillRubrics(blogId, rubrics);
        return rubrics;
    }

    /// <inheritdoc />
    public async Task DeleteRubric(Guid rubricId, CancellationToken ct = default)
    {
        await ThrowIfRubricForbidden(rubricId, ct);

        var userId = _identityProvider.Current.User.UserId;
        await _repository.DeleteRubric(rubricId, userId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Rubric>> GetRubrics(Guid blogId, CancellationToken ct = default)
    {
        await GetAsync(blogId, ct);
        var rubrics = (await _repository.GetRubrics(blogId, ct)).ToArray();
        await _unreadCounters.FillRubrics(blogId, rubrics);
        return rubrics;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Blog>> GetSubscribedBlogs(IEnumerable<Guid> blogIds, CancellationToken ct = default)
    {
        // A subscription outlives the blog's visibility: a blog sent back to
        // premoderation is hidden from its readers, and the list of their blogs
        // leaves it out rather than name a blog that opens as not there.
        var blogs = (await _repository.GetByIds(blogIds, _identityProvider.Current.User.UserId, ct))
            .Where(IsVisibleToViewer)
            .ToArray();
        await _unreadCounters.FillBlogs(blogs);
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
        await _unreadCounters.FillBlogs(blogs);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<Blog> ChangePremoderationAsync(
        string id, ModulePremoderationTransition transition, CancellationToken ct = default)
    {
        // The judge's rank is asked before the read, so probing aliases learns nothing;
        // not GetAsync, which hides a pending blog from the mentor who judges it.
        var isAuthorMove = transition == ModulePremoderationTransition.SubmitForApproval;
        if (!isAuthorMove)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.SetStatusModeration);
        }

        var blog = await GetByIdOrPublicId(id, ct);

        // The judge reads past visibility and pays the mentor rank for it above;
        // the owner's move reads under the viewer scope, so the alias is no oracle.
        if (blog == null || (isAuthorMove && !IsVisibleToViewer(blog)))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        var blogId = blog.Id;
        var currentUserId = _identityProvider.Current.User.UserId;

        // Legality before authorization: an illegal move is a 400 whoever asks.
        var change = ModulePremoderationPolicy.Resolve(
            transition, blog.PremoderationStatus, currentUserId);
        if (isAuthorMove)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.SubmitForApproval, blog);
        }

        var update = new UpdateBlogEntity
        {
            BlogId = blogId,
            ModifiedUtc = _dateTimeProvider.Now,
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
        // Read under the viewer scope: a 400 or a 403 on a hidden blog would tell a
        // stranger that the alias resolves and which status it sits in.
        var blog = await GetByIdOrPublicId(id, ct);
        if (blog == null || !IsVisibleToViewer(blog))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        var blogId = blog.Id;
        var now = _dateTimeProvider.Now;

        // What only a blog answers for; the state machine is ModuleStatusPolicy, shared with games.
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

        // Legality before authorization: an illegal move is a 400 whoever asks.
        var change = ModuleStatusPolicy.Resolve(
            transition,
            new ModuleLifecycle(blog.Status, blog.ClosedReason, blog.ActivatedUtc, blog.ClosedUtc),
            now);
        _intentionManager.ThrowIfForbidden(intention, blog);

        var update = new UpdateBlogEntity
        {
            BlogId = blogId,
            ModifiedUtc = now,
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

    private async Task<Blog> FillCounters(Blog blog)
    {
        await _unreadCounters.FillBlogs(new[] { blog });
        await _unreadCounters.FillBlogRubrics(blog);
        return blog;
    }

    private async Task<Blog?> GetByIdOrPublicId(string id, CancellationToken ct) =>
        Guid.TryParse(id, out var guid)
            ? await _repository.Get(guid, ViewerId, ct)
            : await _repository.GetByPublicId(id, ViewerId, ct);

    private async Task ThrowIfRubricForbidden(Guid rubricId, CancellationToken ct)
    {
        var (rubric, blogId) = await _repository.GetRubric(rubricId, ct);
        if (rubric == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.RubricNotFound);
        }

        var blog = await GetAsync(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.ManageRubrics, blog);
    }

    /// <summary>
    /// Whether the reader may see the blog at all: the private-draft gate and the
    /// premoderation gate, asked rather than thrown.
    /// </summary>
    /// <remarks>
    /// A blog the caller cannot see is a 404 and not a 403, because "not yours"
    /// tells a stranger walking the five-letter aliases that the blog exists. The
    /// one statement of the rule: the viewer's reads of a blog and of what lives
    /// inside it ask it, directly or through <see cref="GetVisibleAsync" /> and
    /// <see cref="IsVisibleToViewerAsync" />. Two reads pass it by on purpose:
    /// <see cref="GetUnfiltered" />, which feeds the intention gates, and the judge's
    /// read in <see cref="ChangePremoderationAsync" />. An action on a blog the
    /// caller can see is still refused with a 403 by its own intention.
    ///
    /// The draft visibility hides a blog while it is a draft and nothing after, as
    /// ModuleVisibility states for every module: a blog started from a private
    /// draft opens to its readers as the lists already show it to them.
    /// </remarks>
    private bool IsVisibleToViewer(Blog blog) =>
        (blog.Status != ModuleStatus.Draft ||
         blog.DraftVisibility != DraftVisibility.Private ||
         _intentionManager.IsAllowed(BlogIntention.ViewDraft, blog)) &&
        (blog.PremoderationStatus == PremoderationStatus.Approved ||
         _intentionManager.IsAllowed(BlogIntention.ViewPremoderationPending, blog));
}
