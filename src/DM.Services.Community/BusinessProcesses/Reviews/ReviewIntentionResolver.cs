using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Reviews;

/// <inheritdoc />
internal class ReviewIntentionResolver :
    IIntentionResolver<ReviewIntention>,
    IIntentionResolver<ReviewIntention, Review>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, ReviewIntention intention) => intention switch
    {
        ReviewIntention.Create => user.Role >= UserRole.SeniorModerator,
        ReviewIntention.ReadUnapproved => user.Role >= UserRole.SeniorModerator,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, ReviewIntention intention, Review target) => intention switch
    {
        ReviewIntention.Edit => !target.Approved && user.UserId == target.Author.UserId,
        ReviewIntention.Approve => !target.Approved && user.Role >= UserRole.SeniorModerator,
        ReviewIntention.Delete => user.Role >= UserRole.SeniorModerator,
        _ => false
    };
}