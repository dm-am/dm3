using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Blogs.Reading;
using DM.Services.Community.BusinessProcesses.Blogs.Writing;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using FluentValidation;
using DbBlog = DM.Services.DataAccess.BusinessObjects.Blogs.Blog;
using DbRubric = DM.Services.DataAccess.BusinessObjects.Blogs.Rubric;
using DbPublication = DM.Services.DataAccess.BusinessObjects.Blogs.Publication;
using DbBlogParticipant = DM.Services.DataAccess.BusinessObjects.Blogs.BlogParticipant;
using DbBlogParticipation = DM.Services.DataAccess.BusinessObjects.Blogs.BlogParticipation;

namespace DM.Services.Community.BusinessProcesses.Blogs;

/// <inheritdoc />
internal class BlogService : IBlogService
{
    private readonly IBlogRepository _readingRepository;
    private readonly IBlogWritingRepository _writingRepository;
    private readonly IUserReadingRepository _userReadingRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IValidator<CreateBlog> _createBlogValidator;
    private readonly IValidator<UpdateBlog> _updateBlogValidator;
    private readonly IValidator<CreateRubric> _createRubricValidator;
    private readonly IValidator<CreatePublication> _createPublicationValidator;
    private readonly IValidator<UpdatePublication> _updatePublicationValidator;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public BlogService(
        IBlogRepository readingRepository,
        IBlogWritingRepository writingRepository,
        IUserReadingRepository userReadingRepository,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IValidator<CreateBlog> createBlogValidator,
        IValidator<UpdateBlog> updateBlogValidator,
        IValidator<CreateRubric> createRubricValidator,
        IValidator<CreatePublication> createPublicationValidator,
        IValidator<UpdatePublication> updatePublicationValidator,
        IMapper mapper,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _readingRepository = readingRepository;
        _writingRepository = writingRepository;
        _userReadingRepository = userReadingRepository;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _createBlogValidator = createBlogValidator;
        _updateBlogValidator = updateBlogValidator;
        _createRubricValidator = createRubricValidator;
        _createPublicationValidator = createPublicationValidator;
        _updatePublicationValidator = updatePublicationValidator;
        _mapper = mapper;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Blog> blogs, PagingResult paging)> GetPublicBlogs(PagingQuery query, CancellationToken ct = default)
    {
        var totalCount = await _readingRepository.CountPublicBlogs(ct);
        var pagingData = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);
        var dbBlogs = await _readingRepository.GetPublicBlogs(pagingData, ct);
        var blogs = _mapper.Map<IEnumerable<Blog>>(dbBlogs);
        return (blogs, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Blog>> GetPopularBlogs(int count = 5, CancellationToken ct = default)
    {
        var dbBlogs = await _readingRepository.GetPopularBlogs(count, ct);
        return _mapper.Map<IEnumerable<Blog>>(dbBlogs);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Blog>> GetUserBlogs(string login, CancellationToken ct = default)
    {
        var user = await _userReadingRepository.GetUser(login);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"User {login} not found");
        }

        var dbBlogs = await _readingRepository.GetUserBlogs(user.UserId, ct);
        return _mapper.Map<IEnumerable<Blog>>(dbBlogs);
    }

    /// <inheritdoc />
    public async Task<Blog> Get(Guid blogId, CancellationToken ct = default)
    {
        var dbBlog = await _readingRepository.Get(blogId, ct);
        if (dbBlog == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Blog not found");
        }

        var blog = _mapper.Map<Blog>(dbBlog);

        if (!blog.IsPublic)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.ViewPrivate, blog);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task<Blog> GetByOwnerLogin(string login, CancellationToken ct = default)
    {
        var dbBlog = await _readingRepository.GetByOwnerLogin(login, ct);
        if (dbBlog == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"Blog for user {login} not found");
        }

        var blog = _mapper.Map<Blog>(dbBlog);

        if (!blog.IsPublic)
        {
            _intentionManager.ThrowIfForbidden(BlogIntention.ViewPrivate, blog);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task<Blog> GetBlog(Guid blogId, CancellationToken ct = default)
    {
        var dbBlog = await _readingRepository.Get(blogId, ct);
        if (dbBlog == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Blog not found");
        }

        return _mapper.Map<Blog>(dbBlog);
    }

    /// <inheritdoc />
    public async Task AddParticipant(Guid blogId, Guid userId, DbBlogParticipation role, CancellationToken ct = default)
    {
        // Check if already a participant
        if (await _writingRepository.IsParticipant(blogId, userId, ct))
        {
            return; // Already a participant, nothing to do
        }

        var participant = new DbBlogParticipant
        {
            ParticipantId = _guidFactory.Create(),
            BlogId = blogId,
            UserId = userId,
            Role = role,
            JoinedUtc = _dateTimeProvider.Now
        };

        await _writingRepository.AddParticipant(participant, ct);
    }

    /// <inheritdoc />
    public async Task<Blog> Create(CreateBlog createBlog, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(BlogIntention.Create);
        await _createBlogValidator.ValidateAndThrowAsync(createBlog, ct);

        var userId = _identityProvider.Current.User.UserId;

        var blog = new DbBlog
        {
            BlogId = _guidFactory.Create(),
            OwnerId = userId,
            Title = createBlog.Title,
            Description = createBlog.Description ?? string.Empty,
            IsPublic = createBlog.IsPublic,
            CommentsEnabled = createBlog.CommentsEnabled,
            CreatedUtc = _dateTimeProvider.Now
        };

        var createdBlog = await _writingRepository.CreateBlog(blog, ct);
        return _mapper.Map<Blog>(createdBlog);
    }

    /// <inheritdoc />
    public async Task<Blog> Update(UpdateBlog updateBlog, CancellationToken ct = default)
    {
        await _updateBlogValidator.ValidateAndThrowAsync(updateBlog, ct);

        var blog = await Get(updateBlog.BlogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        var dbBlog = await _readingRepository.Get(updateBlog.BlogId, ct);
        if (dbBlog == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, $"Blog {updateBlog.BlogId} not found");
        }

        if (updateBlog.Title != null)
            dbBlog.Title = updateBlog.Title;
        if (updateBlog.Description != null)
            dbBlog.Description = updateBlog.Description;
        if (updateBlog.IsPublic.HasValue)
            dbBlog.IsPublic = updateBlog.IsPublic.Value;
        if (updateBlog.CommentsEnabled.HasValue)
            dbBlog.CommentsEnabled = updateBlog.CommentsEnabled.Value;

        var updatedBlog = await _writingRepository.UpdateBlog(dbBlog, ct);
        return _mapper.Map<Blog>(updatedBlog);
    }

    /// <inheritdoc />
    public async Task Delete(Guid blogId, CancellationToken ct = default)
    {
        var blog = await Get(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Delete, blog);

        var userId = _identityProvider.Current.User.UserId;
        await _writingRepository.DeleteBlog(blogId, userId, ct);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Publication> publications, PagingResult paging)> GetPublications(
        Guid blogId, Guid? rubricId, PagingQuery query, CancellationToken ct = default)
    {
        var blog = await Get(blogId, ct);
        var includeUnpublished = _intentionManager.IsAllowed(BlogIntention.ViewPrivate, blog);

        var totalCount = await _readingRepository.CountPublications(blogId, rubricId, includeUnpublished, ct);
        var pagingData = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);

        var dbPublications = await _readingRepository.GetPublications(blogId, rubricId, includeUnpublished, pagingData, ct);
        var publications = _mapper.Map<IEnumerable<Publication>>(dbPublications);
        return (publications, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Publication> GetPublication(Guid publicationId, CancellationToken ct = default)
    {
        var dbPublication = await _readingRepository.GetPublication(publicationId, ct);
        if (dbPublication == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Publication not found");
        }

        var publication = _mapper.Map<Publication>(dbPublication);

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

        var publication = new DbPublication
        {
            PublicationId = _guidFactory.Create(),
            BlogId = createPublication.BlogId,
            RubricId = createPublication.RubricId,
            UserId = userId,
            Title = createPublication.Title,
            Content = createPublication.Content,
            Preview = createPublication.Preview ?? string.Empty,
            IsPublished = createPublication.PublishImmediately,
            PublishedUtc = createPublication.PublishImmediately ? now : null,
            CommentsEnabled = createPublication.CommentsEnabled,
            CreatedUtc = now
        };

        var createdPublication = await _writingRepository.CreatePublication(publication, ct);
        return _mapper.Map<Publication>(createdPublication);
    }

    /// <inheritdoc />
    public async Task<Publication> UpdatePublication(UpdatePublication updatePublication, CancellationToken ct = default)
    {
        await _updatePublicationValidator.ValidateAndThrowAsync(updatePublication, ct);

        var publication = await GetPublication(updatePublication.PublicationId, ct);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Edit, publication);

        var dbPublication = await _readingRepository.GetPublication(updatePublication.PublicationId, ct);
        if (dbPublication == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, $"Publication {updatePublication.PublicationId} not found");
        }

        var userId = _identityProvider.Current.User.UserId;
        var now = _dateTimeProvider.Now;

        if (updatePublication.ClearRubric)
            dbPublication.RubricId = null;
        else if (updatePublication.RubricId.HasValue)
            dbPublication.RubricId = updatePublication.RubricId.Value;

        if (updatePublication.Title != null)
            dbPublication.Title = updatePublication.Title;
        if (updatePublication.Content != null)
            dbPublication.Content = updatePublication.Content;
        if (updatePublication.Preview != null)
            dbPublication.Preview = updatePublication.Preview;
        if (updatePublication.CommentsEnabled.HasValue)
            dbPublication.CommentsEnabled = updatePublication.CommentsEnabled.Value;

        if (updatePublication.IsPublished.HasValue)
        {
            if (updatePublication.IsPublished.Value && !dbPublication.IsPublished)
            {
                _intentionManager.ThrowIfForbidden(PublicationIntention.Publish, publication);
                dbPublication.PublishedUtc = now;
            }
            dbPublication.IsPublished = updatePublication.IsPublished.Value;
        }

        dbPublication.ModifiedByUserId = userId;

        var updatedPublication = await _writingRepository.UpdatePublication(dbPublication, ct);
        return _mapper.Map<Publication>(updatedPublication);
    }

    /// <inheritdoc />
    public async Task DeletePublication(Guid publicationId, CancellationToken ct = default)
    {
        var publication = await GetPublication(publicationId, ct);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Delete, publication);

        var userId = _identityProvider.Current.User.UserId;
        await _writingRepository.DeletePublication(publicationId, userId, ct);
    }

    /// <inheritdoc />
    public async Task<Rubric> CreateRubric(CreateRubric createRubric, CancellationToken ct = default)
    {
        await _createRubricValidator.ValidateAndThrowAsync(createRubric, ct);

        var blog = await Get(createRubric.BlogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreateRubric, blog);

        var rubric = new DbRubric
        {
            RubricId = _guidFactory.Create(),
            BlogId = createRubric.BlogId,
            Title = createRubric.Title,
            SortOrder = createRubric.SortOrder,
            CreatedUtc = _dateTimeProvider.Now
        };

        var createdRubric = await _writingRepository.CreateRubric(rubric, ct);
        return _mapper.Map<Rubric>(createdRubric);
    }

    /// <inheritdoc />
    public async Task DeleteRubric(Guid rubricId, CancellationToken ct = default)
    {
        var rubrics = await _readingRepository.GetRubrics(Guid.Empty, ct);
        var rubric = rubrics.FirstOrDefault(r => r.RubricId == rubricId);
        if (rubric == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Rubric not found");
        }

        var blog = await Get(rubric.BlogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreateRubric, blog);

        var userId = _identityProvider.Current.User.UserId;
        await _writingRepository.DeleteRubric(rubricId, userId, ct);
    }
}
