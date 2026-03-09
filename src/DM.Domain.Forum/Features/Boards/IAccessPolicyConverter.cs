using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Features.Boards;

/// <summary>
/// User role to board policy converter
/// </summary>
internal interface IAccessPolicyConverter
{
    /// <summary>
    /// Converts given user role into available composite board access policy
    /// </summary>
    /// <param name="role">User role</param>
    /// <returns>Board access policy</returns>
    BoardAccessPolicy Convert(UserRole role);
}