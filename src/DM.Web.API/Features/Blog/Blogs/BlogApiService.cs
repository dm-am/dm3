using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Subscriptions;
using DM.Web.API.Shared.Dto;
using ApiPublication = DM.Web.API.Features.Blog.Publications.Publication;
using ApiCreatePublicationRequest = DM.Web.API.Features.Blog.Publications.CreatePublicationRequest;
using ApiUpdatePublicationRequest = DM.Web.API.Features.Blog.Publications.UpdatePublicationRequest;

namespace DM.Web.API.Features.Blog.Blogs;

/// <inheritdoc />
internal class BlogApiService : IBlogApiService
{
    private readonly IBlogService _blogService;
    private readonly IPublicationService _publicationService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly BlogMapper _mapper;

    /// <inheritdoc />
    public BlogApiService(
        IBlogService blogService,
        IPublicationService publicationService,
        ISubscriptionService subscriptionService,
        BlogMapper mapper)
    {
        _blogService = blogService;
        _publicationService = publicationService;
        _subscriptionService = subscriptionService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Blog>> GetBlogs(BlogsQuery query)
    {
        var (blogs, paging) = await QueryBlogs(query);
        return new ListEnvelope<Blog>(blogs.Select(_mapper.ToBlog), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<BlogRef>> GetBlogRefs(BlogsQuery query)
    {
        var (blogs, paging) = await QueryBlogs(query);
        return new ListEnvelope<BlogRef>(blogs.Select(_mapper.ToBlogRef), new PagingInfo(paging));
    }

    /// <summary>
    /// One page of blogs for a query. The full and the lightweight endpoints
    /// differ only in the final projection; keeping the selection here means a
    /// filter added to one cannot go missing from the other, which is what the
    /// two verbatim copies of this block invited.
    /// </summary>
    private async Task<(IEnumerable<DM.Domain.Blog.Features.Blogs.Blog> Blogs, PagingResult Paging)>
        QueryBlogs(BlogsQuery query)
    {
        if (query.Participating == true)
        {
            // Blogs the caller takes part in: owner, mentor, assistant, reader.
            var ownedBlogs = await _blogService.GetOwnBlogsAsync();
            var subscriptions = await _subscriptionService.GetMySubscriptionsAsync(SubscriptionTargetType.Blog);
            var subscribedBlogIds = subscriptions.Select(s => s.TargetId).ToHashSet();

            var subscribedBlogs = subscribedBlogIds.Count > 0
                ? await _blogService.GetSubscribedBlogs(subscribedBlogIds.ToList())
                : Enumerable.Empty<DM.Domain.Blog.Features.Blogs.Blog>();

            var allBlogs = ownedBlogs
                .Concat(subscribedBlogs.Where(b => !ownedBlogs.Any(o => o.Id == b.Id)))
                .ToList();

            // Paged in memory because the two sources are combined here rather
            // than in SQL. The set is one caller's blogs, so it is small.
            var skip = query.Skip;
            var take = query.Take;
            return (allBlogs.Skip(skip).Take(take),
                PagingResult.Create(allBlogs.Count, skip + 1, take));
        }

        return await _blogService.GetPublicBlogs(query, _mapper.ToBlogFilter(query));
    }

    /// <inheritdoc />
    public async Task<Envelope<Blog>> Get(Guid id)
    {
        var blog = await _blogService.GetAsync(id);
        return new Envelope<Blog>(_mapper.ToBlog(blog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Blog>> GetByPublicId(string publicId)
    {
        var blog = await _blogService.GetByPublicIdAsync(publicId);
        return new Envelope<Blog>(_mapper.ToBlog(blog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Blog>> GetByOwnerLogin(string login)
    {
        var blog = await _blogService.GetByOwnerUsernameAsync(login);
        return new Envelope<Blog>(_mapper.ToBlog(blog));
    }

    /// <inheritdoc />
    public async Task<Guid> ResolveId(string idOrPublicId) =>
        Guid.TryParse(idOrPublicId, out var guid)
            ? guid
            : (await GetByPublicId(idOrPublicId)).Resource.Id;

    /// <inheritdoc />
    public async Task<Envelope<Blog>> Create(CreateBlogRequest request)
    {
        var createBlog = _mapper.ToCreateBlog(request);
        var blog = await _blogService.Create(createBlog);
        return new Envelope<Blog>(_mapper.ToBlog(blog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Blog>> Update(Guid id, UpdateBlogRequest request)
    {
        var updateBlog = _mapper.ToUpdateBlog(request);
        updateBlog.BlogId = id;
        var blog = await _blogService.Update(updateBlog);
        return new Envelope<Blog>(_mapper.ToBlog(blog));
    }

    /// <inheritdoc />
    public Task Delete(Guid id) => _blogService.Delete(id);

    /// <inheritdoc />
    public async Task<Envelope<Blog>> ChangePremoderation(string id, BlogPremoderationChangeRequest request)
    {
        var updatedBlog = await _blogService.ChangePremoderationAsync(id, request.Transition);
        return new Envelope<Blog>(_mapper.ToBlog(updatedBlog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Blog>> ChangeStatus(string id, BlogStatusChangeRequest request)
    {
        var updatedBlog = await _blogService.ChangeStatusAsync(id, request.Transition);
        return new Envelope<Blog>(_mapper.ToBlog(updatedBlog));
    }

    /// <inheritdoc />
    public async Task<Envelope<Rubric>> CreateRubric(Guid blogId, CreateRubricRequest request)
    {
        var createRubric = _mapper.ToCreateRubric(request);
        createRubric.BlogId = blogId;
        var rubric = await _blogService.CreateRubric(createRubric);
        return new Envelope<Rubric>(_mapper.ToRubric(rubric));
    }

    /// <inheritdoc />
    public async Task<Envelope<Rubric>> UpdateRubric(Guid rubricId, UpdateRubricRequest request)
    {
        var rubric = await _blogService.UpdateRubric(
            new UpdateRubric { RubricId = rubricId, Title = request.Title });
        return new Envelope<Rubric>(_mapper.ToRubric(rubric));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Rubric>> ReorderRubrics(Guid blogId, ReorderRubricsRequest request)
    {
        var rubrics = await _blogService.ReorderRubrics(blogId, request.RubricIds);
        return new ListEnvelope<Rubric>(rubrics.Select(_mapper.ToRubric));
    }

    /// <inheritdoc />
    public Task DeleteRubric(Guid rubricId) => _blogService.DeleteRubric(rubricId);

    /// <inheritdoc />
    public async Task<ListEnvelope<ApiPublication>> GetPublications(Guid blogId, Guid? rubricId, PagingQuery query)
    {
        var (publications, paging) = await _publicationService.GetPublications(blogId, rubricId, query);
        return new ListEnvelope<ApiPublication>(publications.Select(_mapper.ToPublication), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiPublication>> GetPublication(Guid publicationId)
    {
        var publication = await _publicationService.GetPublication(publicationId);
        return new Envelope<ApiPublication>(_mapper.ToPublication(publication));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiPublication?>> GetUserBestPublication(string username)
    {
        var publication = await _publicationService.GetBestUserPublication(username);
        return new Envelope<ApiPublication?>(
            publication == null ? null : _mapper.ToPublication(publication));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiPublication>> CreatePublication(Guid blogId, ApiCreatePublicationRequest request)
    {
        var createPublication = _mapper.ToCreatePublication(request);
        createPublication.BlogId = blogId;
        var publication = await _publicationService.CreatePublication(createPublication);
        return new Envelope<ApiPublication>(_mapper.ToPublication(publication));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiPublication>> UpdatePublication(Guid publicationId, ApiUpdatePublicationRequest request)
    {
        var updatePublication = _mapper.ToUpdatePublication(request);
        updatePublication.PublicationId = publicationId;
        var publication = await _publicationService.UpdatePublication(updatePublication);
        return new Envelope<ApiPublication>(_mapper.ToPublication(publication));
    }

    /// <inheritdoc />
    public Task DeletePublication(Guid publicationId) => _publicationService.DeletePublication(publicationId);
}
