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
        CreateMap<DM.Domain.Core.Comments.Comment, Comment>();

        CreateMap<Comment, CreateComment>()
            .ForMember(d => d.EntityId, opt => opt.Ignore());

        CreateMap<Comment, UpdateComment>()
            .ForMember(d => d.CommentId, opt => opt.Ignore());

        CreateMap<CreateCommentRequest, CreateComment>()
            .ForMember(d => d.EntityId, opt => opt.Ignore());
    }
}