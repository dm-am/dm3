using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Authorization;

/// <inheritdoc />
internal class UserIntentionResolver : IIntentionResolver<UserIntention, GeneralUser>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, UserIntention intention, GeneralUser target) => intention switch
    {
        UserIntention.Edit => target.UserId == user.UserId,
        UserIntention.WriteMessage => target.UserId != user.UserId,
        UserIntention.Moderate => user.Role >= UserRole.SeniorModerator,
        UserIntention.ReadModNotes or UserIntention.CreateModNote or UserIntention.ViewModeratedProfile =>
            user.Role >= UserRole.Moderator,
        UserIntention.ViewUserIpData => user.Role >= UserRole.Admin,
        _ => false
    };
}
