using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.GameReviews;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc />
/// <remarks>
/// Eligibility (a closed game, newbie, already reviewed) needs the game and the database, so the service checks it.
/// </remarks>
internal class GameReviewIntentionResolver :
    IIntentionResolver<GameReviewIntention>,
    IIntentionResolver<GameReviewIntention, GameReview>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, GameReviewIntention intention) => intention switch
    {
        GameReviewIntention.Create => user.IsAuthenticated,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, GameReviewIntention intention, GameReview target) => intention switch
    {
        GameReviewIntention.Edit => user.UserId == target.Author?.UserId,

        // Removing a stated opinion is a step above removing a comment.
        GameReviewIntention.Delete =>
            user.UserId == target.Author?.UserId || user.Role >= UserRole.SeniorModerator,

        _ => false
    };
}
