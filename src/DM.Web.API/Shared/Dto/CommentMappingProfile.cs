using AutoMapper;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Web.API.Shared.BbRendering;

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
            .AfterMap((src, dest) =>
            {
                // Comment surface allows [mod] blocks. Populate the
                // render-context envelope with the comment author so the JSON
                // converter honors the author's AuthorEdit round-trip and
                // downgrades any other viewer's author_edit request to
                // permission-filtered Display. Without the owner id the
                // converter fails closed (Display for everyone), which is safe
                // but strips the author's own editor round-trip.
                // getCommentForUpdate (GET forum/comments/{id}) sends
                // X-Dm-Audience: author_edit, so this is security-critical:
                // it prevents a non-author from reading hidden [mod] content.
                if (dest.Text is not null)
                    dest.Text.Context = new RenderContextEnvelope
                    {
                        Surface = dest.Text.Surface,
                        PostAuthorUserId = src.Author?.UserId
                    };
            });

        // Request DTOs only. The response DTO used to map into both write
        // models, which is what let a PATCH body carry an author and a like
        // list that nothing read and the document could not mark as ignored.
        CreateMap<CreateCommentRequest, CreateComment>()
            .ForMember(d => d.EntityId, opt => opt.Ignore());

        CreateMap<UpdateCommentRequest, UpdateComment>()
            .ForMember(d => d.CommentId, opt => opt.Ignore());
    }
}
