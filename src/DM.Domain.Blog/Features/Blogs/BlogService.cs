using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
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
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IValidator<CreateBlog> _createBlogValidator;
    private readonly IValidator<UpdateBlog> _updateBlogValidator;
    private readonly IValidator<CreateRubric> _createRubricValidator;
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
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IValidator<CreateBlog> createBlogValidator,
        IValidator<UpdateBlog> updateBlogValidator,
        IValidator<CreateRubric> createRubricValidator,
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
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _createBlogValidator = createBlogValidator;
        _updateBlogValidator = updateBlogValidator;
        _createRubricValidator = createRubricValidator;
        _createPublicationValidator = createPublicationValidator;
        _updatePublicationValidator = updatePublicationValidator;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<BlogModel> blogs, PagingResult paging)> GetPublicBlogs(
        PagingQuery query, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default)
    {
        var totalCount = await _repository.CountPublicBlogs(excludeOwnerIds, ct);
        var pagingData = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);
        var blogs = await _repository.GetPublicBlogs(pagingData, excludeOwnerIds, ct);
        return (blogs, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogModel>> GetPopularBlogs(
        int count = 5, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default)
    {
        return await _repository.GetPopularBlogs(count, excludeOwnerIds, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogModel>> GetUserBlogs(string username, CancellationToken ct = default)
    {
        var user = await _userLookupService.Get(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"User {username} not found");
        }

        return await _repository.GetUserBlogs(user.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<BlogModel> Get(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _repository.Get(blogId, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Blog not found");
        }

        if (blog.DraftVisibility == DraftVisibility.Private)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.ViewDraft, blog);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task<BlogModel> GetByOwnerUsername(string username, CancellationToken ct = default)
    {
        var blog = await _repository.GetByOwnerUsername(username, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"Blog for user {username} not found");
        }

        if (blog.DraftVisibility == DraftVisibility.Private)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.ViewDraft, blog);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task<BlogModel> GetBlog(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _repository.Get(blogId, ct);
        if (blog == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Blog not found");
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
    public async Task<BlogModel> Create(CreateBlog createBlog, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(BlogIntention.Create);
        await _createBlogValidator.ValidateAndThrowAsync(createBlog, ct);

        var userId = _identityProvider.Current.User.UserId;
        var entity = new CreateBlogEntity
        {
            BlogId = _guidFactory.Create(),
            OwnerId = userId,
            Title = createBlog.Title,
            Description = createBlog.Description,
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

        return createdBlog;
    }

    /// <inheritdoc />
    public async Task<BlogModel> Update(UpdateBlog updateBlog, CancellationToken ct = default)
    {
        await _updateBlogValidator.ValidateAndThrowAsync(updateBlog, ct);

        var blog = await Get(updateBlog.BlogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        var entity = new UpdateBlogEntity
        {
            BlogId = updateBlog.BlogId,
            Title = updateBlog.Title,
            Description = updateBlog.Description,
            DraftVisibility = updateBlog.DraftVisibility,
            CommentsEnabled = updateBlog.CommentsEnabled,
            UpdatedUtc = _dateTimeProvider.Now
        };
        return await _repository.UpdateBlog(entity, ct);
    }

    /// <inheritdoc />
    public async Task Delete(Guid blogId, CancellationToken ct = default)
    {
        var blog = await Get(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Delete, blog);

        var userId = _identityProvider.Current.User.UserId;
        await _repository.DeleteBlog(blogId, userId, ct);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Publication> publications, PagingResult paging)> GetPublications(
        Guid blogId, Guid? rubricId, PagingQuery query, CancellationToken ct = default)
    {
        var blog = await Get(blogId, ct);
        var includeUnpublished = _intentionManager.IsAllowed(BlogIntention.ViewDraft, blog);

        var totalCount = await _repository.CountPublications(blogId, rubricId, includeUnpublished, ct);
        var pagingData = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);

        var publications = await _repository.GetPublications(blogId, rubricId, includeUnpublished, pagingData, ct);
        return (publications, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Publication> GetPublication(Guid publicationId, CancellationToken ct = default)
    {
        var publication = await _repository.GetPublication(publicationId, ct);
        if (publication == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Publication not found");
        }

        if (!publication.IsPublished)
        {
            _intentionManager.ThrowIfForbidden(PublicationIntention.ViewDraft, publication);
        }

        return publication;
    }

    /// <inheritdoc />
    public async Task<Publication> CreatePublication(CreatePublication createPublication, CancellationToken ct = default)
    {
        await _createPublicationValidator.ValidateAndThrowAsync(createPublication, ct);

        var blog = await Get(createPublication.BlogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreatePublication, blog);

        var userId = _identityProvider.Current.User.UserId;
        var now = _dateTimeProvider.Now;
        var entity = new CreatePublicationEntity
        {
            PublicationId = _guidFactory.Create(),
            BlogId = createPublication.BlogId,
            RubricId = createPublication.RubricId,
            AuthorId = userId,
            Title = createPublication.Title,
            Content = createPublication.Content,
            Preview = createPublication.Preview,
            CommentsEnabled = createPublication.CommentsEnabled,
            PublishImmediately = createPublication.PublishImmediately,
            CreatedUtc = now
        };
        var createdPublication = await _repository.CreatePublication(entity, ct);
        await _eventProducer.Send(EventType.NewPublication, createdPublication.Id);
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
            Content = updatePublication.Content,
            Preview = updatePublication.Preview,
            CommentsEnabled = updatePublication.CommentsEnabled,
            IsPublished = updatePublication.IsPublished,
            UpdatedUtc = _dateTimeProvider.Now
        };
        var updatedPublication = await _repository.UpdatePublication(entity, ct);
        await _eventProducer.Send(EventType.ChangedPublication, updatedPublication.Id);
        return updatedPublication;
    }

    /// <inheritdoc />
    public async Task DeletePublication(Guid publicationId, CancellationToken ct = default)
    {
        var publication = await GetPublication(publicationId, ct);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Delete, publication);

        var userId = _identityProvider.Current.User.UserId;
        await _repository.DeletePublication(publicationId, userId, ct);
        await _eventProducer.Send(EventType.DeletedPublication, publicationId);
    }

    /// <inheritdoc />
    public async Task<Rubric> CreateRubric(CreateRubric createRubric, CancellationToken ct = default)
    {
        await _createRubricValidator.ValidateAndThrowAsync(createRubric, ct);

        var blog = await Get(createRubric.BlogId, ct);
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
    public async Task DeleteRubric(Guid rubricId, CancellationToken ct = default)
    {
        var (rubric, blogId) = await _repository.GetRubric(rubricId, ct);
        if (rubric == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Rubric not found");
        }

        var blog = await Get(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreateRubric, blog);

        var userId = _identityProvider.Current.User.UserId;
        await _repository.DeleteRubric(rubricId, userId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Rubric>> GetRubrics(Guid blogId, CancellationToken ct = default)
    {
        // Verify blog exists and user has access
        await Get(blogId, ct);
        return await _repository.GetRubrics(blogId, ct);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Subscribe(Guid blogId, CancellationToken ct = default)
    {
        var blog = await GetBlog(blogId, ct);
        var userId = _identityProvider.Current.User.UserId;

        // Cannot subscribe to own blog
        if (blog.Author.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Cannot subscribe to your own blog");
        }

        // Check draft visibility (drafts with private visibility require invitation)
        if (blog.DraftVisibility == DraftVisibility.Private)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "This draft blog has private visibility. You need an invitation to subscribe.");
        }

        // Use BlogSubscriptionService for readers
        await _subscriptionService.Subscribe(blogId, ct);
        return _identityProvider.Current.User;
    }

    /// <inheritdoc />
    public async Task Unsubscribe(Guid blogId, CancellationToken ct = default)
    {
        var blog = await GetBlog(blogId, ct);
        var userId = _identityProvider.Current.User.UserId;

        // Owner cannot unsubscribe from their own blog
        if (blog.Author.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Blog owner cannot unsubscribe from their own blog");
        }

        // Use BlogSubscriptionService for readers
        await _subscriptionService.Unsubscribe(blogId, ct);
    }

    /// <inheritdoc />
    public async Task Leave(Guid blogId, CancellationToken ct = default)
    {
        var blog = await GetBlog(blogId, ct);
        var userId = _identityProvider.Current.User.UserId;

        // Owner cannot leave their own blog
        if (blog.Author.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Blog owner cannot leave their own blog");
        }

        // Remove as assistant
        var removed = await _repository.RemoveAssistant(blogId, userId, ct);
        if (!removed)
        {
            throw new HttpException(HttpStatusCode.NotFound, "You are not an assistant in this blog");
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetReaders(Guid blogId, CancellationToken ct = default)
    {
        await Get(blogId, ct);
        // Get subscribers via BlogSubscriptionService
        return await _subscriptionService.GetReaders(blogId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogUser>> GetAssistants(Guid blogId, CancellationToken ct = default)
    {
        await Get(blogId, ct);
        return await _repository.GetAssistantsWithJoinDate(blogId, ct);
    }

    /// <inheritdoc />
    public async Task RemoveAssistant(Guid blogId, string username, CancellationToken ct = default)
    {
        var blog = await GetBlog(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        var removed = await _repository.RemoveAssistantByUsername(blogId, username, ct);
        if (!removed)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"Assistant '{username}' not found in this blog");
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogModel>> GetSubscribedBlogs(IEnumerable<Guid> blogIds, CancellationToken ct = default)
    {
        return await _repository.GetByIds(blogIds, ct);
    }
}
