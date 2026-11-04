using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Caching;

/// <summary>
/// Keys of the cached reads shared by more than one module.
/// </summary>
/// <remarks>
/// A misspelled key fails silently (a stale read or a cache that never hits), so
/// every module spells the keys here.
/// </remarks>
public static class CacheKeys
{
    /// <summary>Profile as read by name.</summary>
    /// <param name="username">Login, in any case: the key is normalised here.</param>
    public static string UserDetails(string username) =>
        $"user_details_{username.ToLowerInvariant()}";

    /// <summary>Profile as read by identifier.</summary>
    /// <param name="userId">User identifier</param>
    public static string UserDetails(Guid userId) => $"user_details_{userId}";

    /// <summary>Everyone holding a role — the staff lists.</summary>
    /// <param name="role">Role being listed</param>
    public static string UsersByRole(UserRole role) => $"users_by_role_{role}";
}
