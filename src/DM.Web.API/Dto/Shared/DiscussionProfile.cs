using AutoMapper;
using ServiceComment = DM.Services.Common.Dto.Comment;

namespace DM.Web.API.Dto.Shared;

/// <summary>
/// AutoMapper profile for discussion DTOs
/// </summary>
internal class DiscussionProfile : Profile
{
    /// <inheritdoc />
    public DiscussionProfile()
    {
        CreateMap<ServiceComment, DiscussionComment>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc))
            .ForMember(d => d.UpdatedUtc, s => s.MapFrom(c => c.ModifiedUtc))
            .ForMember(d => d.LikesCount, s => s.MapFrom(c => c.Likes != null ? System.Linq.Enumerable.Count(c.Likes) : 0))
            .ForMember(d => d.IsLikedByMe, s => s.Ignore())
            .ForMember(d => d.CanEdit, s => s.Ignore())
            .ForMember(d => d.CanDelete, s => s.Ignore())
            .ForMember(d => d.CanLike, s => s.Ignore());
    }
}
