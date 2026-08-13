using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Authorization;

/// <summary>
/// Resolver for moderation intentions (without target)
/// </summary>
internal class ModerationIntentionResolver : IIntentionResolver<ModerationIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, ModerationIntention intention) =>
        intention switch
        {
            // Moderator+ actions
            ModerationIntention.ViewModNotes => user.Role >= UserRole.Moderator,
            ModerationIntention.CreateModNote => user.Role >= UserRole.Moderator,
            ModerationIntention.EditModNote => user.Role >= UserRole.Moderator,
            ModerationIntention.DeleteModNote => user.Role >= UserRole.Moderator,

            // Admin only actions
            ModerationIntention.SetUserRole => user.Role >= UserRole.Admin,

            // Moderate user profile (SeniorModerator+)
            ModerationIntention.ModerateUserProfile => user.Role >= UserRole.SeniorModerator,

            // Tag management (SeniorModerator+)
            ModerationIntention.ManageTags => user.Role >= UserRole.SeniorModerator,

            _ => false
        };
}
