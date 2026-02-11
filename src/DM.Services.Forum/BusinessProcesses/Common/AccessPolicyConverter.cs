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

        var result = BoardAccessPolicy.Guest | BoardAccessPolicy.RegularUser;

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
            result |= BoardAccessPolicy.Moderator;
        }

        if (role >= UserRole.Mentor)
        {
            result |= BoardAccessPolicy.Mentor;
        }

        return result;
    }
}
