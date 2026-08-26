using System.Linq;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;
using ServiceComment = DM.Domain.Core.Comments.Comment;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Compile-time mapper for discussion DTOs. The per-reader flags (likes,
/// edit/delete/like rights) stay with CommentReading, which knows the
/// reader; this maps only what the comment itself carries.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
internal partial class DiscussionMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public DiscussionMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain comment to the discussion row.
    ///
    /// No render-context envelope on Text - a decision, not an omission. The
    /// discussion listing is display-only: no editor seeds from it - every
    /// surface edits through its single-comment GET, which maps via
    /// CommentMapper and does carry the envelope. A client-sent author_edit
    /// header on the listing therefore degrades to Display in the converter,
    /// which fails closed: nothing leaks, and nothing reads the degraded
    /// answer.
    /// </summary>
    public DiscussionComment ToDiscussionComment(ServiceComment comment)
    {
        var result = ToDiscussionCommentCore(comment);
        result.LikesCount = comment.Likes != null ? comment.Likes.Count() : 0;
        return result;
    }

    [MapperIgnoreTarget(nameof(DiscussionComment.LikesCount))]
    [MapperIgnoreTarget(nameof(DiscussionComment.IsLikedByMe))]
    [MapperIgnoreTarget(nameof(DiscussionComment.CanEdit))]
    [MapperIgnoreTarget(nameof(DiscussionComment.CanDelete))]
    [MapperIgnoreTarget(nameof(DiscussionComment.CanLike))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial DiscussionComment ToDiscussionCommentCore(ServiceComment comment);
}
