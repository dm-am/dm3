using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Identity;
using DM.Web.API.Features.Blog.Publications;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using SvcBlog = DM.Domain.Blog.Features.Blogs.Blog;
using SvcBlogFilter = DM.Domain.Blog.Features.Blogs.BlogFilter;
using SvcRubric = DM.Domain.Blog.Features.Blogs.Rubric;
using SvcPublication = DM.Domain.Blog.Features.Publications.Publication;
using SvcCreateBlog = DM.Domain.Blog.Features.Blogs.CreateBlog;
using SvcUpdateBlog = DM.Domain.Blog.Features.Blogs.UpdateBlog;
using SvcCreateRubric = DM.Domain.Blog.Features.Blogs.CreateRubric;
using SvcCreatePublication = DM.Domain.Blog.Features.Publications.CreatePublication;
using SvcUpdatePublication = DM.Domain.Blog.Features.Publications.UpdatePublication;

namespace DM.Web.API.Features.Blog.Blogs;

/// <summary>
/// Compile-time mapper for blog API DTOs. The render-context envelope on the
/// description is an explicit step of <see cref="ToBlog"/>.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
[UseStaticMapper(typeof(UserRefMappers))]
public partial class BlogMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    private readonly IIdentityProvider _identityProvider;

    public BlogMapper(UserMapper userMapper, IIdentityProvider identityProvider)
    {
        _userMapper = userMapper;
        _identityProvider = identityProvider;
    }

    /// <summary>
    /// Domain blog to the lightweight sidebar/menu reference
    /// </summary>
    public BlogRef ToBlogRef(SvcBlog blog)
    {
        if (blog == null)
        {
            return null!;
        }

        return ToBlogRefCore(blog);
    }

    /// <summary>
    /// Domain blog to the full list/card DTO, with the owner bound to the
    /// description for the settings editor's AuthorEdit read.
    /// </summary>
    public Blog ToBlog(SvcBlog blog)
    {
        if (blog == null)
        {
            return null!;
        }

        var result = ToBlogCore(blog);
        result.ViewerBlacklisted = AnonymousIdentity.Of(_identityProvider.Current?.User) is { } viewerId &&
                                   blog.IsBlacklisted(viewerId);
        if (blog.Author is not null)
        {
            result.Description?.BindAuthor(blog.Author.UserId,
                intentions => intentions.IsAllowed(BlogIntention.EditSettings, blog));
        }

        return result;
    }

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial Rubric ToRubric(SvcRubric rubric);

    /// <summary>
    /// Domain publication to the API one, with the author bound to the content:
    /// the publication editor reads its source with the author_edit audience.
    /// </summary>
    public Publication ToPublication(SvcPublication publication)
    {
        if (publication == null)
        {
            return null!;
        }

        var result = ToPublicationCore(publication);
        result.Content?.BindAuthor(publication.Author?.UserId,
            intentions => intentions.IsAllowed(PublicationIntention.Edit, publication));
        return result;
    }

    /// <summary>
    /// Query string to the domain filter. The three fields the domain fills
    /// itself stay behind.
    /// </summary>
    [MapperIgnoreTarget(nameof(SvcBlogFilter.HostUserIds))]
    [MapperIgnoreTarget(nameof(SvcBlogFilter.CurrentUserId))]
    [MapperIgnoreTarget(nameof(SvcBlogFilter.ExcludeOwnerIds))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial SvcBlogFilter ToBlogFilter(BlogsQuery query);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial SvcCreateBlog ToCreateBlog(CreateBlogRequest request);

    /// <summary>
    /// Update request to the write model. BlogId comes from the route, the
    /// service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(SvcUpdateBlog.BlogId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial SvcUpdateBlog ToUpdateBlog(UpdateBlogRequest request);

    [MapperIgnoreTarget(nameof(SvcCreateRubric.BlogId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial SvcCreateRubric ToCreateRubric(CreateRubricRequest request);

    [MapperIgnoreTarget(nameof(SvcCreatePublication.BlogId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial SvcCreatePublication ToCreatePublication(CreatePublicationRequest request);

    [MapperIgnoreTarget(nameof(SvcUpdatePublication.PublicationId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial SvcUpdatePublication ToUpdatePublication(UpdatePublicationRequest request);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial BlogRef ToBlogRefCore(SvcBlog blog);

    // The viewer's standing is composed in ToBlog, what he may do in BlogApiService.
    [MapperIgnoreTarget(nameof(Blog.ViewerBlacklisted))]
    [MapperIgnoreTarget(nameof(Blog.Viewer))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Blog ToBlogCore(SvcBlog blog);

    // A publication without a rubric hands a null over from a `= null!` member.
    [MapProperty(nameof(SvcPublication.Rubric), nameof(Publication.Rubric), Use = nameof(ToRubricOrNull))]
    [MapperIgnoreTarget(nameof(Publication.Viewer))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Publication ToPublicationCore(SvcPublication publication);

    private Rubric ToRubricOrNull(SvcRubric? rubric) =>
        rubric == null ? null! : ToRubric(rubric);
}
