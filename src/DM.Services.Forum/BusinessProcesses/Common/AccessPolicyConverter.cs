using DM.Services.Core.Dto.Enums;

namespace DM.Services.Forum.BusinessProcesses.Common;

/// <inheritdoc />
internal class AccessPolicyConverter : IAccessPolicyConverter
{
    /// <inheritdoc />
    public ForumAccessPolicy Convert(UserRole role)
    {
        if (role == UserRole.Guest)
        {
            return ForumAccessPolicy.Guest;
        }

        var result = ForumAccessPolicy.Guest | ForumAccessPolicy.Player;

        if (role >= UserRole.Admin)
        {
            result |= ForumAccessPolicy.Administrator;
        }

        if (role >= UserRole.SeniorModerator)
        {
            result |= ForumAccessPolicy.SeniorModerator;
        }

        if (role >= UserRole.Moderator)
        {
            result |= ForumAccessPolicy.RegularModerator;
        }

        if (role >= UserRole.Mentor)
        {
            result |= ForumAccessPolicy.MentorModerator;
        }

        return result;
    }
}
