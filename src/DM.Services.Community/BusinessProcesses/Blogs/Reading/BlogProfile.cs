using System.Linq;
using AutoMapper;
using DbBlog = DM.Services.DataAccess.BusinessObjects.Blogs.Blog;
using DbRubric = DM.Services.DataAccess.BusinessObjects.Blogs.Rubric;
using DbPublication = DM.Services.DataAccess.BusinessObjects.Blogs.Publication;

namespace DM.Services.Community.BusinessProcesses.Blogs.Reading;

/// <summary>
/// AutoMapper profile for blog entities
/// </summary>
internal class BlogProfile : Profile
{
    /// <inheritdoc />
    public BlogProfile()
    {
        CreateMap<DbBlog, Blog>()
            .ForMember(d => d.Id, s => s.MapFrom(b => b.BlogId))
            .ForMember(d => d.CreatedAt, s => s.MapFrom(b => b.CreatedUtc))
            .ForMember(d => d.UpdatedAt, s => s.MapFrom(b => b.UpdatedUtc))
            .ForMember(d => d.CommentsCount, s => s.MapFrom(b =>
                b.Publications != null ? b.Publications.Sum(p => p.CommentCount) : 0));

        CreateMap<DbRubric, Rubric>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.RubricId));

        CreateMap<DbPublication, Publication>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PublicationId))
            .ForMember(d => d.Author, s => s.MapFrom(p => p.Author))
            .ForMember(d => d.CreatedAt, s => s.MapFrom(p => p.CreatedUtc))
            .ForMember(d => d.ModifiedAt, s => s.MapFrom(p => p.ModifiedUtc))
            .ForMember(d => d.PublishedAt, s => s.MapFrom(p => p.PublishedUtc))
            .ForMember(d => d.Likes, s => s.Ignore()); // Likes fetched via EntityType+EntityId pattern
    }
}
