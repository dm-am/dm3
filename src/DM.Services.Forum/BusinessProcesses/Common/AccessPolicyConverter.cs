using DM.Services.Core.Dto.Enums;

namespace DM.Services.Forum.BusinessProcesses.Common;

/// <inheritdoc />
internal class AccessPolicyConverter : IAccessPolicyConverter
{
    /// <inheritdoc />
    public BoardAccessPolicy Convert(UserRole role)
    {
        if (role == UserRole.Guest)
        {
            return BoardAccessPolicy.Guest;
        }

        var result = BoardAccessPolicy.Guest | BoardAccessPolicy.Player;

        if (role >= UserRole.Admin)
        {
            result |= BoardAccessPolicy.Administrator;
        }

        if (role >= UserRole.SeniorModerator)
        {
            result |= BoardAccessPolicy.SeniorModerator;
        }

        if (role >= UserRole.Moderator)
        {
            result |= BoardAccessPolicy.RegularModerator;
        }

        if (role >= UserRole.Mentor)
        {
            result |= BoardAccessPolicy.MentorModerator;
        }

        return result;
    }
}
