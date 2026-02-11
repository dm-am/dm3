using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Users;

/// <inheritdoc />
internal class UserIntentionResolver : IIntentionResolver<UserIntention, GeneralUser>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, UserIntention intention, GeneralUser target) => intention switch
    {
        UserIntention.Edit => target.UserId == user.UserId,
        UserIntention.WriteMessage => target.UserId != user.UserId,
        UserIntention.Moderate => user.Role >= UserRole.SeniorModerator,
        UserIntention.ReadModNotes => user.Role >= UserRole.Moderator,
        UserIntention.CreateModNote => user.Role >= UserRole.Moderator,
        // Own notes can be edited/deleted by any moderator, others' notes require SeniorModerator+
        UserIntention.EditModNote => user.Role >= UserRole.Moderator,
        UserIntention.DeleteModNote => user.Role >= UserRole.Moderator,
        UserIntention.ViewModerationProfile => user.Role >= UserRole.Moderator,
        UserIntention.ViewUserIpData => user.Role >= UserRole.Admin,
        _ => false
    };
}

/// <inheritdoc />
internal class UserIntentionResolverWithoutTarget : IIntentionResolver<UserIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, UserIntention intention) => intention switch
    {
        UserIntention.ViewPendingUsers => user.Role >= UserRole.SeniorModerator,
        _ => false
    };
}