using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Profiles;

public interface IModeratedProfileRepository
{
    /// <summary>
    /// Update the Info field of the user
    /// </summary>
    Task UpdateUserInfo(string username, string info, CancellationToken ct = default);

    Task SetUserRole(string username, UserRole role, CancellationToken ct = default);

    /// <summary>
    /// Put the user under moderation watch, or take them off it
    /// </summary>
    Task SetModerationWatch(string username, bool underWatch, CancellationToken ct = default);
}
