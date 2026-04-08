using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.GameReviews;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc />
/// <remarks>
/// Note: Eligibility checks (CanReviewGame, IsNewbie) are performed in the service layer
/// since they require async database queries. The IntentionResolver only handles simple authorization rules.
/// </remarks>
internal class GameReviewIntentionResolver :
    IIntentionResolver<GameReviewIntention>,
    IIntentionResolver<GameReviewIntention, GameReview>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, GameReviewIntention intention) => intention switch
    {
        // Create requires authentication - actual eligibility checked in service
        GameReviewIntention.Create => user.IsAuthenticated,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, GameReviewIntention intention, GameReview target) => intention switch
    {
        // Edit is allowed for the author
        GameReviewIntention.Edit => user.UserId == target.Author.UserId,

        // Delete is allowed for author or moderator
        GameReviewIntention.Delete => user.UserId == target.Author.UserId || user.Role >= UserRole.Moderator,

        _ => false
    };
}
