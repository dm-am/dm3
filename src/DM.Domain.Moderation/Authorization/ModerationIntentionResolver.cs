using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Authorization;

internal class ModerationIntentionResolver : IIntentionResolver<ModerationIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, ModerationIntention intention) =>
        intention switch
        {
            // The moderation watch is the manual half of keeping the violators list, so it
            // stands at the rank of warnings; editing the profile text is a rank higher.
            ModerationIntention.ViewModNotes or ModerationIntention.CreateModNote or
                ModerationIntention.EditModNote or ModerationIntention.DeleteModNote or
                ModerationIntention.SetModerationWatch => user.Role >= UserRole.Moderator,

            ModerationIntention.ModerateUserProfile or ModerationIntention.ManageTags =>
                user.Role >= UserRole.SeniorModerator,

            ModerationIntention.SetUserRole => user.Role >= UserRole.Admin,

            _ => false
        };
}
