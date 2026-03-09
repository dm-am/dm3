using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using ApiPublication = DM.Web.API.Features.Blog.Publications.Publication;
using ApiCreatePublicationRequest = DM.Web.API.Features.Blog.Publications.CreatePublicationRequest;
using ApiUpdatePublicationRequest = DM.Web.API.Features.Blog.Publications.UpdatePublicationRequest;

namespace DM.Web.API.Features.Blog.Blogs;

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
    public async Task<ListEnvelope<Blog>> GetPublicBlogs(PagingQuery query)
    {
        var (blogs, paging) = await _blogService.GetPublicBlogs(query);
        return new ListEnvelope<Blog>(blogs.Select(_mapper.Map<Blog>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Blog>> GetPopularBlogs()
    {
        var blogs = await _blogService.GetPopularBlogs();
        return new ListEnvelope<Blog>(blogs.Select(_mapper.Map<Blog>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Blog>> GetUserBlogs(string login)
    {
        var blogs = await _blogService.GetUserBlogs(login);
        return new ListEnvelope<Blog>(blogs.Select(_mapper.Map<Blog>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Blog>> Get(Guid id)
    {
        var blog = await _blogService.Get(id);
        return new Envelope<Blog>(_mapper.Map<Blog>(blog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Blog>> GetByOwnerLogin(string login)
    {
        var blog = await _blogService.GetByOwnerUsername(login);
        return new Envelope<Blog>(_mapper.Map<Blog>(blog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Blog>> Create(CreateBlogRequest request)
    {
        var createBlog = _mapper.Map<CreateBlog>(request);
        var blog = await _blogService.Create(createBlog);
        return new Envelope<Blog>(_mapper.Map<Blog>(blog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Blog>> Update(Guid id, UpdateBlogRequest request)
    {
        var updateBlog = _mapper.Map<UpdateBlog>(request);
        updateBlog.BlogId = id;
        var blog = await _blogService.Update(updateBlog);
        return new Envelope<Blog>(_mapper.Map<Blog>(blog));
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
    public async Task<ListEnvelope<ApiPublication>> GetPublications(Guid blogId, Guid? rubricId, PagingQuery query)
    {
        var (publications, paging) = await _blogService.GetPublications(blogId, rubricId, query);
        return new ListEnvelope<ApiPublication>(publications.Select(_mapper.Map<ApiPublication>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiPublication>> GetPublication(Guid publicationId)
    {
        var publication = await _blogService.GetPublication(publicationId);
        return new Envelope<ApiPublication>(_mapper.Map<ApiPublication>(publication));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiPublication>> CreatePublication(Guid blogId, ApiCreatePublicationRequest request)
    {
        var createPublication = _mapper.Map<CreatePublication>(request);
        createPublication.BlogId = blogId;
        var publication = await _blogService.CreatePublication(createPublication);
        return new Envelope<ApiPublication>(_mapper.Map<ApiPublication>(publication));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiPublication>> UpdatePublication(Guid publicationId, ApiUpdatePublicationRequest request)
    {
        var updatePublication = _mapper.Map<UpdatePublication>(request);
        updatePublication.PublicationId = publicationId;
        var publication = await _blogService.UpdatePublication(updatePublication);
        return new Envelope<ApiPublication>(_mapper.Map<ApiPublication>(publication));
    }

    /// <inheritdoc />
    public Task DeletePublication(Guid publicationId) => _blogService.DeletePublication(publicationId);
}
