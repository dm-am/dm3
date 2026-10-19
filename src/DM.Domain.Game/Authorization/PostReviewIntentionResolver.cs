using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.PostReviews;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc />
/// <remarks>
/// Eligibility (newbie, a recent review in the game) needs the database, so the service checks it.
/// </remarks>
internal class PostReviewIntentionResolver :
    IIntentionResolver<PostReviewIntention>,
    IIntentionResolver<PostReviewIntention, PostReview>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PostReviewIntention intention) => intention switch
    {
        PostReviewIntention.Create => user.IsAuthenticated,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PostReviewIntention intention, PostReview target) => intention switch
    {
        PostReviewIntention.Edit => user.UserId == target.Author?.UserId,

        // Removing a stated opinion is a step above removing a comment.
        PostReviewIntention.Delete =>
            user.UserId == target.Author?.UserId || user.Role >= UserRole.SeniorModerator,

        _ => false
    };
}
