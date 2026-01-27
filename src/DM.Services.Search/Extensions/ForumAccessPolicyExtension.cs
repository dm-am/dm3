using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Search.Extensions;

/// <summary>
/// Extensions for board entities indexing
/// </summary>
public static class BoardAccessPolicyExtension
{
    /// <summary>
    /// Get list of atomic roles that are authorized to perform board policy action
    /// </summary>
    /// <param name="boardAccessPolicy">Board access policy</param>
    /// <returns>List of user roles</returns>
    public static IEnumerable<UserRole> GetAuthorizedRoles(this BoardAccessPolicy boardAccessPolicy)
    {
        if (boardAccessPolicy.HasFlag(BoardAccessPolicy.Guest))
        {
            yield return UserRole.Guest;
            yield return UserRole.RegularUser;
        }

        if (boardAccessPolicy.HasFlag(BoardAccessPolicy.MentorModerator))
        {
            yield return UserRole.Mentor;
        }

        if (boardAccessPolicy.HasFlag(BoardAccessPolicy.BoardModerator))
        {
            yield return UserRole.Moderator;
        }

        if (boardAccessPolicy.HasFlag(BoardAccessPolicy.SeniorModerator))
        {
            yield return UserRole.SeniorModerator;
        }

        if (boardAccessPolicy.HasFlag(BoardAccessPolicy.Administrator))
        {
            yield return UserRole.Admin;
        }
    }
}
