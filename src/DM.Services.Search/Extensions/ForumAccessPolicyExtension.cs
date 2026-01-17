using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Search.Extensions;

/// <summary>
/// Extensions for forum entities indexing
/// </summary>
public static class ForumAccessPolicyExtension
{
    /// <summary>
    /// Get list of atomic roles that are authorized to perform forum policy action
    /// </summary>
    /// <param name="forumAccessPolicy">Forum access policy</param>
    /// <returns>List of user roles</returns>
    public static IEnumerable<UserRole> GetAuthorizedRoles(this ForumAccessPolicy forumAccessPolicy)
    {
        if (forumAccessPolicy.HasFlag(ForumAccessPolicy.Guest))
        {
            yield return UserRole.Guest;
            yield return UserRole.RegularUser;
        }

        if (forumAccessPolicy.HasFlag(ForumAccessPolicy.MentorModerator))
        {
            yield return UserRole.Mentor;
        }

        if (forumAccessPolicy.HasFlag(ForumAccessPolicy.ForumModerator))
        {
            yield return UserRole.Moderator;
        }

        if (forumAccessPolicy.HasFlag(ForumAccessPolicy.SeniorModerator))
        {
            yield return UserRole.SeniorModerator;
        }

        if (forumAccessPolicy.HasFlag(ForumAccessPolicy.Administrator))
        {
            yield return UserRole.Admin;
        }
    }
}
