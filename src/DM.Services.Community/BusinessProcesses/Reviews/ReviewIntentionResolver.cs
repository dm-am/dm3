using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Reviews;

/// <inheritdoc />
/// <remarks>
/// Note: For User/Game reviews, eligibility checks (HavePlayedTogether, CanReviewGame)
/// are performed in the service layer since they require async database queries.
/// The IntentionResolver only handles simple authorization rules.
/// </remarks>
internal class ReviewIntentionResolver :
    IIntentionResolver<ReviewIntention>,
    IIntentionResolver<ReviewIntention, Review>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, ReviewIntention intention) => intention switch
    {
        // Platform reviews
        ReviewIntention.Create => user.Role >= UserRole.SeniorModerator,
        ReviewIntention.ReadUnapproved => user.Role >= UserRole.SeniorModerator,

        // User/Game reviews - basic auth check only
        // Actual eligibility is checked in the service layer
        ReviewIntention.CreateUserReview => user.IsAuthenticated,
        ReviewIntention.CreateGameReview => user.IsAuthenticated,

        // Post reviews - basic auth check only
        // Eligibility (not own post, not newbie, cooldown) is checked in service layer
        ReviewIntention.CreatePostReview => user.IsAuthenticated,

        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, ReviewIntention intention, Review target) => intention switch
    {
        // Platform reviews
        ReviewIntention.Edit => !target.Approved && user.UserId == target.Author.UserId,
        ReviewIntention.Approve => !target.Approved && user.Role >= UserRole.SeniorModerator,
        ReviewIntention.Delete => user.Role >= UserRole.SeniorModerator,

        // User reviews
        ReviewIntention.EditUserReview => user.UserId == target.Author.UserId,
        ReviewIntention.DeleteUserReview =>
            user.UserId == target.Author.UserId || user.Role >= UserRole.Moderator,

        // Game reviews
        ReviewIntention.EditGameReview => user.UserId == target.Author.UserId,
        ReviewIntention.DeleteGameReview =>
            user.UserId == target.Author.UserId || user.Role >= UserRole.Moderator,

        // Post reviews (no edit - only delete)
        ReviewIntention.DeletePostReview =>
            user.UserId == target.Author.UserId || user.Role >= UserRole.Moderator,

        _ => false
    };
}