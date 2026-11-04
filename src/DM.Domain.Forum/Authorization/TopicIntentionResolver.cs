using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Topics;

namespace DM.Domain.Forum.Authorization;

/// <inheritdoc />
internal class TopicIntentionResolver : IIntentionResolver<TopicIntention, Topic>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, TopicIntention intention, Topic target)
    {
        bool IsBoardStaff() => target.Board.ModeratorIds.Contains(user.UserId) || user.Role >= UserRole.Moderator;

        return intention switch
        {
            TopicIntention.CreateComment when user.IsAuthenticated => !target.IsClosed && user.MaySpeak(),
            // Editing your own topic is speech and the ban silences it; editing somebody
            // else's is moderation. A topic the author closed stays theirs to edit.
            TopicIntention.Edit when user.IsAuthenticated && target.Author?.UserId == user.UserId =>
                user.MaySpeak() && (!target.IsClosed || target.ClosedByAuthor || IsBoardStaff()),
            TopicIntention.Edit when user.IsAuthenticated =>
                IsBoardStaff(),
            TopicIntention.Like when user.IsAuthenticated => target.Author?.UserId != user.UserId,
            // What a moderator closed the author does not reopen, or the closing would last one click.
            TopicIntention.Close when user.IsAuthenticated =>
                BoardIntentionResolver.AdministratesTopics(user, target.Board) ||
                (target.Author?.UserId == user.UserId && user.MaySpeak() &&
                 (!target.IsClosed || target.ClosedByAuthor)),
            _ => false
        };
    }
}
