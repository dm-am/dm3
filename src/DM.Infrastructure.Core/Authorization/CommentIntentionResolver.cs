using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Core.Authorization;

/// <summary>
/// Who may edit, delete and like a comment
/// </summary>
/// <remarks>
/// Here and not in a domain module: a comment belongs to no one module, and the
/// kernel that declares it is scanned by nobody.
/// </remarks>
internal class CommentIntentionResolver : IIntentionResolver<CommentIntention, Comment>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, CommentIntention intention, Comment target) => intention switch
    {
        CommentIntention.Edit or CommentIntention.Delete when user.IsAuthenticated =>
            user.Role >= UserRole.Moderator || target.Author?.UserId == user.UserId,
        CommentIntention.Like when user.IsAuthenticated => target.Author?.UserId != user.UserId,
        _ => false
    };
}
