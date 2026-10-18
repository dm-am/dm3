using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Topics;

namespace DM.Domain.Forum.Authorization;

/// <inheritdoc />
internal class TopicIntentionResolver : IIntentionResolver<TopicIntention, Topic>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, TopicIntention intention, Topic target) => intention switch
    {
        TopicIntention.CreateComment when user.IsAuthenticated => !target.IsClosed && user.MaySpeak(),
        // Editing your own topic is speech, and an ordinary ban silences it on
        // the forum exactly as it silences the comment one line above. Editing
        // somebody else's is moderation, and a ban takes no moderator tool
        // away, so the ban is asked of the author only. Pinning and moving a
        // topic hang off this same intention and stay open for the same reason.
        // A topic the author closed themselves stays theirs to edit: they could
        // reopen it, edit and close it again, and the form sends all three at once.
        TopicIntention.Edit when user.IsAuthenticated && target.Author?.UserId == user.UserId =>
            user.MaySpeak() && (!target.IsClosed || target.ClosedByAuthor ||
                                target.Board.ModeratorIds.Contains(user.UserId) || user.Role >= UserRole.Moderator),
        TopicIntention.Edit when user.IsAuthenticated =>
            target.Board.ModeratorIds.Contains(user.UserId) || user.Role >= UserRole.Moderator,
        TopicIntention.Like when user.IsAuthenticated => target.Author?.UserId != user.UserId,
        // The author closes and reopens their own topic, and it is speech like
        // editing it: an ordinary ban takes it away. What a moderator closed the
        // author does not reopen, or the closing would last one click. The
        // board's moderators and the administrators close and reopen any topic,
        // ban or not.
        TopicIntention.Close when user.IsAuthenticated =>
            BoardIntentionResolver.AdministratesTopics(user, target.Board) ||
            (target.Author?.UserId == user.UserId && user.MaySpeak() &&
             (!target.IsClosed || target.ClosedByAuthor)),
        _ => false
    };
}
