using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Authorization;

/// <summary>
/// Resolver for community-wide intentions
/// </summary>
internal class CommunityIntentionResolver : IIntentionResolver<CommunityIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, CommunityIntention intention) =>
        intention switch
        {
            CommunityIntention.ViewPendingUsers => user.Role >= UserRole.SeniorModerator,
            _ => false
        };
}
