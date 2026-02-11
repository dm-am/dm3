using System.Linq;
using AutoMapper;
using SvcBlog = DM.Services.Community.BusinessProcesses.Blogs.Reading.Blog;
using SvcRubric = DM.Services.Community.BusinessProcesses.Blogs.Reading.Rubric;
using SvcPublication = DM.Services.Community.BusinessProcesses.Blogs.Reading.Publication;
using SvcCreateBlog = DM.Services.Community.BusinessProcesses.Blogs.Writing.CreateBlog;
using SvcUpdateBlog = DM.Services.Community.BusinessProcesses.Blogs.Writing.UpdateBlog;
using SvcCreateRubric = DM.Services.Community.BusinessProcesses.Blogs.Writing.CreateRubric;
using SvcCreatePublication = DM.Services.Community.BusinessProcesses.Blogs.Writing.CreatePublication;
using SvcUpdatePublication = DM.Services.Community.BusinessProcesses.Blogs.Writing.UpdatePublication;

namespace DM.Web.API.Dto.Blogs;

/// <summary>
/// AutoMapper profile for Blog API DTOs
/// </summary>
internal class BlogProfile : Profile
{
    /// <inheritdoc />
    public BlogProfile()
    {
        // Service to API
        CreateMap<SvcBlog, Blog>()
            .ForMember(d => d.SubscribersCount, s => s.MapFrom(b => b.Participants.Count()));
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
