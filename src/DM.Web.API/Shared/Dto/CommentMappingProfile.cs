using AutoMapper;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Mapping profile from Service DTO to API DTO for commentaries
/// </summary>
internal class CommentMappingProfile : Profile
{
    /// <inheritdoc />
    public CommentMappingProfile()
    {
        CreateMap<DM.Domain.Core.Comments.Comment, Comment>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc))
            .ForMember(d => d.UpdatedUtc, s => s.MapFrom(c => c.ModifiedUtc));

        CreateMap<Comment, CreateComment>();
        CreateMap<Comment, UpdateComment>();
        CreateMap<CreateCommentRequest, CreateComment>();
    }
}