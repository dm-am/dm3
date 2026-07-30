using System.Linq;
using AutoMapper;
using DM.Web.API.Features.Blog.Publications;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using SvcBlog = DM.Domain.Blog.Features.Blogs.Blog;
using SvcBlogFilter = DM.Domain.Blog.Features.Blogs.BlogFilter;
using SvcRubric = DM.Domain.Blog.Features.Blogs.Rubric;
using SvcPublication = DM.Domain.Blog.Features.Blogs.Publication;
using SvcCreateBlog = DM.Domain.Blog.Features.Blogs.CreateBlog;
using SvcUpdateBlog = DM.Domain.Blog.Features.Blogs.UpdateBlog;
using SvcCreateRubric = DM.Domain.Blog.Features.Blogs.CreateRubric;
using SvcCreatePublication = DM.Domain.Blog.Features.Blogs.CreatePublication;
using SvcUpdatePublication = DM.Domain.Blog.Features.Blogs.UpdatePublication;

namespace DM.Web.API.Features.Blog.Blogs;

/// <summary>
/// AutoMapper profile for Blog API DTOs
/// </summary>
internal class BlogMappingProfile : Profile
{
    /// <inheritdoc />
    public BlogMappingProfile()
    {
        // Note: BlogAssistantInfo → UserRef mapping is in UserRefMappingProfile

        // BlogRef mapping (BASE - for sidebars/menus)
        CreateMap<SvcBlog, BlogRef>()
            .ForMember(d => d.Author, s => s.MapFrom(b => b.Author))
            .ForMember(d => d.SubscribersCount, s => s.MapFrom(b => b.SubscribersCount))
            .ForMember(d => d.SubscriberUsernames, s => s.MapFrom(b => b.SubscriberUsernames))
            .ForMember(d => d.ActiveSubscribersCount, s => s.MapFrom(b => b.ActiveSubscribersCount))
            .ForMember(d => d.Assistants, s => s.MapFrom(b => b.Assistants));

        // Blog mapping (extends BlogRef with rubrics and additional fields)
        CreateMap<SvcBlog, Blog>()
            .IncludeBase<SvcBlog, BlogRef>()
            // Rubrics, Description, Mentor, etc. map by convention.
            // Populate the render-context envelope on the description so the
            // JSON converter honors the owner's AuthorEdit round-trip (the
            // settings editor sends X-Dm-Audience: author_edit to load the raw
            // BBCode source) and downgrades any other viewer's author_edit
            // request to permission-filtered Display. Inherited by BlogDetails
            // via IncludeBase.
            .AfterMap((src, dest) =>
            {
                if (dest.Description is not null && src.Author is not null)
                {
                    dest.Description.Context = new RenderContextEnvelope
                    {
                        Surface = dest.Description.Surface,
                        PostAuthorUserId = src.Author.UserId
                    };
                }
            });

        CreateMap<SvcRubric, Rubric>();
        CreateMap<SvcPublication, Publication>();

        // Query string to domain filter. Mapped by name rather than by hand so
        // a filter added to both sides needs no third edit here, and so the
        // three fields the domain fills itself have to be named to be skipped.
        CreateMap<BlogsQuery, SvcBlogFilter>()
            .ForMember(d => d.HostUserIds, opt => opt.Ignore())
            .ForMember(d => d.CurrentUserId, opt => opt.Ignore())
            .ForMember(d => d.ExcludeOwnerIds, opt => opt.Ignore());

        // API Request to Service DTO
        CreateMap<CreateBlogRequest, SvcCreateBlog>();
        CreateMap<UpdateBlogRequest, SvcUpdateBlog>()
            .ForMember(d => d.BlogId, opt => opt.Ignore());
        CreateMap<CreateRubricRequest, SvcCreateRubric>()
            .ForMember(d => d.BlogId, opt => opt.Ignore());
        CreateMap<CreatePublicationRequest, SvcCreatePublication>()
            .ForMember(d => d.BlogId, opt => opt.Ignore());
        CreateMap<UpdatePublicationRequest, SvcUpdatePublication>()
            .ForMember(d => d.PublicationId, opt => opt.Ignore());
    }
}
