using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Gaming.Dto.Output;

namespace DM.Services.Gaming.Authorization;

/// <inheritdoc />
internal class VoteIntentionResolver :
    IIntentionResolver<VoteIntention, Post>,
    IIntentionResolver<VoteIntention, Vote>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, VoteIntention intention, Post target)
    {
        return intention switch
        {
            // Can vote on any post except your own
            VoteIntention.Create => user.IsAuthenticated && target.Author.UserId != user.UserId,
            _ => false
        };
    }

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, VoteIntention intention, Vote target)
    {
        return intention switch
        {
            // Only the vote author can delete their vote
            VoteIntention.Delete => target.Author.UserId == user.UserId,
            _ => false
        };
    }
}
