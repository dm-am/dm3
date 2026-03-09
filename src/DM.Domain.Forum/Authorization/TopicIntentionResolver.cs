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
        TopicIntention.CreateComment when user.IsAuthenticated => !target.IsClosed,
        TopicIntention.Edit when user.IsAuthenticated => target.Author.UserId == user.UserId && !target.IsClosed ||
                                                         target.Board.ModeratorIds.Contains(user.UserId) || user.Role >= UserRole.Moderator,
        TopicIntention.Like when user.IsAuthenticated => target.Author.UserId != user.UserId,
        _ => false
    };
}