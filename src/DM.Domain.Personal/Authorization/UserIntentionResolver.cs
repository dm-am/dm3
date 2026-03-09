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
        UserIntention.ReadModNotes => user.Role >= UserRole.Moderator,
        UserIntention.CreateModNote => user.Role >= UserRole.Moderator,
        // Own notes can be edited/deleted by any moderator, others' notes require SeniorModerator+
        UserIntention.EditModNote => user.Role >= UserRole.Moderator,
        UserIntention.DeleteModNote => user.Role >= UserRole.Moderator,
        UserIntention.ViewModeratedProfile => user.Role >= UserRole.Moderator,
        UserIntention.ViewUserIpData => user.Role >= UserRole.Admin,
        _ => false
    };
}

/// <inheritdoc />
internal class UserIntentionResolverWithoutTarget : IIntentionResolver<UserIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, UserIntention intention) => intention switch
    {
        UserIntention.ViewPendingUsers => user.Role >= UserRole.SeniorModerator,
        _ => false
    };
}
