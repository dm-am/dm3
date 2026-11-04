using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Authorization;

/// <inheritdoc />
/// <remarks>
/// Eligibility (played together, newbie status) needs async reads and is the service's.
/// </remarks>
internal class UserEndorsementIntentionResolver :
    IIntentionResolver<UserEndorsementIntention>,
    IIntentionResolver<UserEndorsementIntention, UserEndorsement>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, UserEndorsementIntention intention) => intention switch
    {
        UserEndorsementIntention.Create => user.IsAuthenticated,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, UserEndorsementIntention intention, UserEndorsement target) =>
        intention switch
        {
            UserEndorsementIntention.Edit => user.UserId == target.Author.UserId,
            // Deleting somebody's stated opinion is a step above deleting a comment,
            // and AUTHORIZATION.md puts it there with profile moderation: the author
            // themselves, or a senior moderator.
            UserEndorsementIntention.Delete =>
                user.UserId == target.Author.UserId || user.Role >= UserRole.SeniorModerator,
            _ => false
        };
}
