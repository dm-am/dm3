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
internal partial class BlogMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public BlogMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
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
    /// Domain blog to the full list/card DTO. The explicit envelope step
    /// replaces the AutoMapper AfterMap: the owner owns the description, so
    /// the envelope is what lets the JSON converter honor the owner's
    /// AuthorEdit round-trip (the settings editor sends X-Dm-Audience:
    /// author_edit to load the raw BBCode source) while downgrading any
    /// other viewer's author_edit request to permission-filtered Display.
    /// </summary>
    public Blog ToBlog(SvcBlog blog)
    {
        if (blog == null)
        {
            return null!;
        }

        var result = ToBlogCore(blog);
        if (result.Description is not null && blog.Author is not null)
        {
            result.Description.Context = new RenderContextEnvelope
            {
                Surface = result.Description.Surface,
                PostAuthorUserId = blog.Author.UserId
            };
        }

        return result;
    }

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial Rubric ToRubric(SvcRubric rubric);

    /// <summary>
    /// Domain publication to the API one.
    ///
    /// No render-context envelope on Content - a decision, not an omission.
    /// The publication editor (PublicationEdit.vue) seeds from the display
    /// render through the editor-internal htmlToBbcode; no author_edit
    /// request exists on the publication path at all, so an envelope here
    /// would have no reader, and a stray author_edit header degrades to
    /// Display in the converter - failing closed. If the editor moves to an
    /// AuthorEdit fetch, the envelope moves here with it.
    /// </summary>
    public Publication ToPublication(SvcPublication publication)
    {
        if (publication == null)
        {
            return null!;
        }

        return ToPublicationCore(publication);
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

    // The author maps by name and by convention: a blog is owned by exactly
    // one user, the column is not nullable and the projection hydrates it, so
    // there is nothing here for a null-tolerant hop to tolerate.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial BlogRef ToBlogRefCore(SvcBlog blog);

    // The description envelope is composed in the wrapper.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Blog ToBlogCore(SvcBlog blog);

    // A publication without a rubric hands a null over from a `= null!`
    // member; the guard keeps null flowing through.
    [MapProperty(nameof(SvcPublication.Rubric), nameof(Publication.Rubric), Use = nameof(ToRubricOrNull))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Publication ToPublicationCore(SvcPublication publication);

    private Rubric ToRubricOrNull(SvcRubric? rubric) =>
        rubric == null ? null! : ToRubric(rubric);
}
