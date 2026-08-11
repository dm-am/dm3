using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.PostReviews;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc />
/// <remarks>
/// Note: Eligibility checks (IsNewbie, HasRecentReviewInGame) are performed in the service layer
/// since they require async database queries. The IntentionResolver only handles simple authorization rules.
/// </remarks>
internal class PostReviewIntentionResolver :
    IIntentionResolver<PostReviewIntention>,
    IIntentionResolver<PostReviewIntention, PostReview>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PostReviewIntention intention) => intention switch
    {
        // Create requires authentication - actual eligibility checked in service
        PostReviewIntention.Create => user.IsAuthenticated,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PostReviewIntention intention, PostReview target) => intention switch
    {
        // Edit is allowed for the author
        PostReviewIntention.Edit => user.UserId == target.Author.UserId,

        // Deleting somebody's stated opinion is a step above deleting a comment,
        // and AUTHORIZATION.md puts it there with profile moderation: the author
        // themselves, or a senior moderator.
        PostReviewIntention.Delete =>
            user.UserId == target.Author.UserId || user.Role >= UserRole.SeniorModerator,

        _ => false
    };
}
