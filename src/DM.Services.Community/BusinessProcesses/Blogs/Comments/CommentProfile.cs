using AutoMapper;
using DbComment = DM.Services.DataAccess.BusinessObjects.Common.Comment;
using DM.Services.Community.BusinessProcesses.Blogs.Comments.Dto;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments;

/// <summary>
/// AutoMapper profile for publication comment entities
/// </summary>
internal class CommentProfile : Profile
{
    /// <inheritdoc />
    public CommentProfile()
    {
        CreateMap<DbComment, CommentToDelete>()
            .IncludeBase<DbComment, Services.Common.Dto.Comment>()
            .ForMember(d => d.PublicationId, s => s.MapFrom(c => c.EntityId))
            .ForMember(d => d.EntityCommentCount, s => s.MapFrom(c => c.Publication != null ? c.Publication.CommentCount : 0));
    }
}
