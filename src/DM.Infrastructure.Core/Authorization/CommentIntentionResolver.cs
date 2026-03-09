using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Core.Authorization;

/// <inheritdoc />
internal class CommentIntentionResolver : IIntentionResolver<CommentIntention, Comment>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, CommentIntention intention, Comment target) => intention switch
    {
        CommentIntention.Edit when user.IsAuthenticated => user.Role >= UserRole.Moderator ||
                                                           target.Author.UserId == user.UserId,
        CommentIntention.Delete when user.IsAuthenticated => user.Role >= UserRole.Moderator ||
                                                             target.Author.UserId == user.UserId,
        CommentIntention.Like when user.IsAuthenticated => target.Author.UserId != user.UserId,
        _ => false
    };
}
