using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Features.Boards;

/// <summary>
/// Extension methods for BoardAccessPolicy enum
/// </summary>
public static class BoardAccessPolicyExtension
{
    /// <summary>
    /// Get all user roles that are authorized by the given access policy
    /// </summary>
    /// <param name="policy">Board access policy</param>
    /// <returns>Enumerable of authorized user roles</returns>
    public static IEnumerable<UserRole> GetAuthorizedRoles(this BoardAccessPolicy policy)
    {
        if (policy.HasFlag(BoardAccessPolicy.Guest))
        {
            yield return UserRole.Guest;
        }

        if (policy.HasFlag(BoardAccessPolicy.RegularUser))
        {
            yield return UserRole.RegularUser;
        }

        if (policy.HasFlag(BoardAccessPolicy.Mentor))
        {
            yield return UserRole.Mentor;
        }

        if (policy.HasFlag(BoardAccessPolicy.Moderator))
        {
            yield return UserRole.Moderator;
        }

        if (policy.HasFlag(BoardAccessPolicy.SeniorModerator))
        {
            yield return UserRole.SeniorModerator;
        }

        if (policy.HasFlag(BoardAccessPolicy.Administrator))
        {
            yield return UserRole.Admin;
        }
    }
}
