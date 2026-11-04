using DM.Domain.Core.Authorization;
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
public partial class CommentMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public CommentMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain comment to the API comment, with the envelope every comment needs
    /// for [mod] rendering.
    /// </summary>
    public Comment ToComment(DomainComment comment)
    {
        var result = ToCommentCore(comment);

        // AuthorEdit goes to the author and to whoever may edit the comment
        // (moderation), Display to everyone else.
        result.Text?.BindAuthor(comment.Author?.UserId,
            intentions => intentions.IsAllowed(CommentIntention.Edit, comment));

        return result;
    }

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Comment ToCommentCore(DomainComment comment);

    /// <summary>
    /// Create request to the write model. EntityId comes from the route, the
    /// service sets it. Request DTOs only: a response DTO never maps into a write model.
    /// </summary>
    [MapperIgnoreTarget(nameof(CreateComment.EntityId))]
    public partial CreateComment ToCreateComment(CommentRequest request);

    /// <summary>
    /// Update request to the write model. CommentId comes from the route,
    /// the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(UpdateComment.CommentId))]
    public partial UpdateComment ToUpdateComment(CommentRequest request);
}
