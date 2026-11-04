using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Features.Boards;

internal interface IAccessPolicyConverter
{
    /// <summary>
    /// Converts given user role into available composite board access policy
    /// </summary>
    BoardAccessPolicy Convert(UserRole role);
}
