using System.Linq;
using AutoMapper;
using DM.Web.API.Features.Blog.Publications;
using DM.Web.API.Features.Community.Users;
using SvcBlog = DM.Domain.Blog.Features.Blogs.Blog;
using SvcBlogDetails = DM.Domain.Blog.Features.Blogs.BlogDetails;
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
            .ForMember(d => d.SubscribersCount, s => s.MapFrom(b => b.SubscriberIds.Count))
            .ForMember(d => d.SubscriberUsernames, s => s.MapFrom(b => b.SubscriberUsernames))
            .ForMember(d => d.ActiveSubscribersCount, s => s.MapFrom(b => b.ActiveSubscribersCount))
            .ForMember(d => d.Assistants, s => s.MapFrom(b => b.Assistants));

        // Blog mapping (extends BlogRef with rubrics and additional fields)
        CreateMap<SvcBlog, Blog>()
            .IncludeBase<SvcBlog, BlogRef>();
            // Rubrics, Description, etc. map by convention

        // BlogDetails mapping (extends Blog with subscribers and full assistants)
        CreateMap<SvcBlogDetails, BlogDetails>()
            .IncludeBase<SvcBlog, Blog>()
            .ForMember(d => d.Subscribers, s => s.MapFrom(b => b.Subscribers))
            .ForMember(d => d.FullAssistants, s => s.MapFrom(b => b.FullAssistants.Select(a => a.User)));

        CreateMap<SvcRubric, Rubric>();
        CreateMap<SvcPublication, Publication>();

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
