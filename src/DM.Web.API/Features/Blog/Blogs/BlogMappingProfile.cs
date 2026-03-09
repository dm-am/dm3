using AutoMapper;
using DM.Web.API.Features.Blog.Publications;
using SvcBlog = DM.Domain.Blog.Features.Blogs.BlogModel;
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
        // Service to API
        CreateMap<SvcBlog, Blog>()
            .ForMember(d => d.Owner, s => s.MapFrom(b => b.Author))
            .ForMember(d => d.SubscribersCount, s => s.MapFrom(b => b.SubscriberIds.Count));
        CreateMap<SvcRubric, Rubric>();
        CreateMap<SvcPublication, Publication>();

        // API Request to Service DTO
        CreateMap<CreateBlogRequest, SvcCreateBlog>();
        CreateMap<UpdateBlogRequest, SvcUpdateBlog>();
        CreateMap<CreateRubricRequest, SvcCreateRubric>();
        CreateMap<CreatePublicationRequest, SvcCreatePublication>();
        CreateMap<UpdatePublicationRequest, SvcUpdatePublication>();
    }
}
