using System.Linq;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Forum.Dto.Output;

namespace DM.Services.Forum.Authorization;

/// <inheritdoc />
internal class TopicIntentionResolver : IIntentionResolver<TopicIntention, Topic>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, TopicIntention intention, Topic target) => intention switch
    {
        TopicIntention.CreateComment when user.IsAuthenticated => !target.IsClosed,
        TopicIntention.Edit when user.IsAuthenticated => target.Author.UserId == user.UserId && !target.IsClosed ||
                                                         target.Board.ModeratorIds.Contains(user.UserId) || user.Role >= UserRole.Admin,
        TopicIntention.Like when user.IsAuthenticated => target.Author.UserId != user.UserId,
        _ => false
    };
}