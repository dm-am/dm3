using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Caching;

/// <summary>
/// Keys of the cached reads shared by more than one module.
/// </summary>
/// <remarks>
/// A profile is cached under two keys — by name and by identifier — because both
/// are read paths, and it is written and invalidated from three modules. Spelled
/// as string literals in ten places, they had already drifted: removing an avatar
/// invalidated a key nothing ever wrote, and left the by-name entry alone, so the
/// profile page kept showing the deleted avatar for the whole minute of its TTL.
///
/// A misspelled key fails silently in both directions — a stale read or a cache
/// that never hits — which is what makes this worth a shared spelling rather than
/// care.
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
