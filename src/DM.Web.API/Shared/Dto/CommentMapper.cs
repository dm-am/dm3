using DM.Domain.Core.Comments;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;
using DomainComment = DM.Domain.Core.Comments.Comment;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Compile-time mapper from service DTO to API DTO for commentaries
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
internal partial class CommentMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public CommentMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain comment to the API comment. The explicit envelope step replaces
    /// the AutoMapper AfterMap - it must run on every comment that crosses
    /// the API, or [mod] rendering silently breaks for the other viewer.
    /// </summary>
    public Comment ToComment(DomainComment comment)
    {
        var result = ToCommentCore(comment);

        // Comment surface allows [mod] blocks. Populate the render-context
        // envelope with the comment author so the JSON converter honors the
        // author's AuthorEdit round-trip and downgrades any other viewer's
        // author_edit request to permission-filtered Display. Without the
        // owner id the converter fails closed (Display for everyone), which
        // is safe but strips the author's own editor round-trip.
        // getCommentForUpdate (GET forum/comments/{id}) sends
        // X-Dm-Audience: author_edit, so this is security-critical:
        // it prevents a non-author from reading hidden [mod] content.
        if (result.Text is not null)
        {
            result.Text.Context = new RenderContextEnvelope
            {
                Surface = result.Text.Surface,
                PostAuthorUserId = comment.Author?.UserId
            };
        }

        return result;
    }

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Comment ToCommentCore(DomainComment comment);

    /// <summary>
    /// Create request to the write model. EntityId comes from the route, the
    /// service sets it. Request DTOs only: the response DTO must never map
    /// into a write model - that is what let a PATCH body carry an author
    /// and a like list that nothing read.
    /// </summary>
    [MapperIgnoreTarget(nameof(CreateComment.EntityId))]
    public partial CreateComment ToCreateComment(CreateCommentRequest request);

    /// <summary>
    /// Update request to the write model. CommentId comes from the route,
    /// the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(UpdateComment.CommentId))]
    public partial UpdateComment ToUpdateComment(UpdateCommentRequest request);
}
