using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Features.Boards;

internal interface IAccessPolicyConverter
{
    BoardAccessPolicy Convert(UserRole role);
}
