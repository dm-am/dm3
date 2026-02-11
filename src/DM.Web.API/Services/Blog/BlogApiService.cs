using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Blogs;
using DM.Services.Community.BusinessProcesses.Blogs.Writing;
using DM.Services.Core.Dto;
using DM.Web.API.Dto.Blogs;
using DM.Web.API.Dto.Contracts;

namespace DM.Web.API.Services.Blog;

/// <inheritdoc />
internal class BlogApiService : IBlogApiService
{
    private readonly IBlogService _blogService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public BlogApiService(
        IBlogService blogService,
        IMapper mapper)
    {
        _blogService = blogService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Dto.Blogs.Blog>> GetPublicBlogs(PagingQuery query)
    {
        var (blogs, paging) = await _blogService.GetPublicBlogs(query);
        return new ListEnvelope<Dto.Blogs.Blog>(blogs.Select(_mapper.Map<Dto.Blogs.Blog>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Dto.Blogs.Blog>> GetPopularBlogs()
    {
        var blogs = await _blogService.GetPopularBlogs();
        return new ListEnvelope<Dto.Blogs.Blog>(blogs.Select(_mapper.Map<Dto.Blogs.Blog>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Dto.Blogs.Blog>> GetUserBlogs(string login)
    {
        var blogs = await _blogService.GetUserBlogs(login);
        return new ListEnvelope<Dto.Blogs.Blog>(blogs.Select(_mapper.Map<Dto.Blogs.Blog>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Dto.Blogs.Blog>> Get(Guid id)
    {
        var blog = await _blogService.Get(id);
        return new Envelope<Dto.Blogs.Blog>(_mapper.Map<Dto.Blogs.Blog>(blog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Dto.Blogs.Blog>> GetByOwnerLogin(string login)
    {
        var blog = await _blogService.GetByOwnerLogin(login);
        return new Envelope<Dto.Blogs.Blog>(_mapper.Map<Dto.Blogs.Blog>(blog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Dto.Blogs.Blog>> Create(CreateBlogRequest request)
    {
        var createBlog = _mapper.Map<CreateBlog>(request);
        var blog = await _blogService.Create(createBlog);
        return new Envelope<Dto.Blogs.Blog>(_mapper.Map<Dto.Blogs.Blog>(blog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Dto.Blogs.Blog>> Update(Guid id, UpdateBlogRequest request)
    {
        var updateBlog = _mapper.Map<UpdateBlog>(request);
        updateBlog.BlogId = id;
        var blog = await _blogService.Update(updateBlog);
        return new Envelope<Dto.Blogs.Blog>(_mapper.Map<Dto.Blogs.Blog>(blog));
    }

    /// <inheritdoc />
    public Task Delete(Guid id) => _blogService.Delete(id);

    /// <inheritdoc />
    public async Task<Envelope<Rubric>> CreateRubric(Guid blogId, CreateRubricRequest request)
    {
        var createRubric = _mapper.Map<CreateRubric>(request);
        createRubric.BlogId = blogId;
        var rubric = await _blogService.CreateRubric(createRubric);
        return new Envelope<Rubric>(_mapper.Map<Rubric>(rubric));
    }

    /// <inheritdoc />
    public Task DeleteRubric(Guid rubricId) => _blogService.DeleteRubric(rubricId);

    /// <inheritdoc />
    public async Task<ListEnvelope<Publication>> GetPublications(Guid blogId, Guid? rubricId, PagingQuery query)
    {
        var (publications, paging) = await _blogService.GetPublications(blogId, rubricId, query);
        return new ListEnvelope<Publication>(publications.Select(_mapper.Map<Publication>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Publication>> GetPublication(Guid publicationId)
    {
        var publication = await _blogService.GetPublication(publicationId);
        return new Envelope<Publication>(_mapper.Map<Publication>(publication));
    }

    /// <inheritdoc />
    public async Task<Envelope<Publication>> CreatePublication(Guid blogId, CreatePublicationRequest request)
    {
        var createPublication = _mapper.Map<CreatePublication>(request);
        createPublication.BlogId = blogId;
        var publication = await _blogService.CreatePublication(createPublication);
        return new Envelope<Publication>(_mapper.Map<Publication>(publication));
    }

    /// <inheritdoc />
    public async Task<Envelope<Publication>> UpdatePublication(Guid publicationId, UpdatePublicationRequest request)
    {
        var updatePublication = _mapper.Map<UpdatePublication>(request);
        updatePublication.PublicationId = publicationId;
        var publication = await _blogService.UpdatePublication(updatePublication);
        return new Envelope<Publication>(_mapper.Map<Publication>(publication));
    }

    /// <inheritdoc />
    public Task DeletePublication(Guid publicationId) => _blogService.DeletePublication(publicationId);
}
