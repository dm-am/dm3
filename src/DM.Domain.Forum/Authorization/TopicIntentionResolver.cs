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
        // away, so the ban is asked of the author only. Closing, pinning and
        // moving a topic hang off this same intention and stay open for the
        // same reason.
        TopicIntention.Edit when user.IsAuthenticated && target.Author.UserId == user.UserId =>
            user.MaySpeak() && (!target.IsClosed ||
                                target.Board.ModeratorIds.Contains(user.UserId) || user.Role >= UserRole.Moderator),
        TopicIntention.Edit when user.IsAuthenticated =>
            target.Board.ModeratorIds.Contains(user.UserId) || user.Role >= UserRole.Moderator,
        TopicIntention.Like when user.IsAuthenticated => target.Author.UserId != user.UserId,
        _ => false
    };
}