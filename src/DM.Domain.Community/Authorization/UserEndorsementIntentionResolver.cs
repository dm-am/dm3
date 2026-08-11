using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Authorization;

/// <inheritdoc />
/// <remarks>
/// Note: Eligibility checks (HavePlayedTogether, newbie status) are performed in the service layer
/// since they require async database queries. The IntentionResolver only handles simple authorization rules.
/// </remarks>
internal class UserEndorsementIntentionResolver :
    IIntentionResolver<UserEndorsementIntention>,
    IIntentionResolver<UserEndorsementIntention, UserEndorsement>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, UserEndorsementIntention intention) => intention switch
    {
        // Basic auth check only - eligibility (played together, post count) checked in service
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
