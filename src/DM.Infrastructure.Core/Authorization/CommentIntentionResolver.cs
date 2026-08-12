using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Core.Authorization;

/// <summary>
/// Who may edit, delete and like a comment
/// </summary>
/// <remarks>
/// Every other intention resolver lives in the domain module that owns its
/// target. This one has no such module: a comment hangs off a topic, a game, a
/// blog and a publication alike, so <see cref="Comment" /> and
/// <see cref="CommentIntention" /> are kernel types, and the kernel is the one
/// project nothing may be referenced from — a resolver placed there would be
/// scanned by nobody. Infrastructure.Core is where it can both see the kernel
/// and be registered. Uploads are outside a module for the same reason and the
/// exception is recorded next to the rule it bends; this is the second and last
/// such case.
/// </remarks>
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
