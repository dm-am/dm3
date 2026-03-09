using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Profiles;

/// <summary>
/// Repository for moderated user profile operations
/// </summary>
public interface IModeratedProfileRepository
{
    /// <summary>
    /// Update user info field (moderator action)
    /// </summary>
    Task UpdateUserInfo(string username, string info, CancellationToken ct = default);

    /// <summary>
    /// Set user role (moderator/admin action)
    /// </summary>
    Task SetUserRole(string username, UserRole role, CancellationToken ct = default);
}
