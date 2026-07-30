using System;

namespace DM.Domain.Core.Identity;

/// <summary>
/// System user (Robot Administrator) for automated actions.
/// This user cannot log in and is used for system-generated content like auto-bans.
/// </summary>
public static class SystemUser
{
    /// <summary>
    /// System user identifier (fixed, well-known GUID)
    /// </summary>
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// System user display name
    /// </summary>
    public const string Username = "Робот-Администратор";

    /// <summary>
    /// System user email (not used for login)
    /// </summary>
    public const string Email = "system@dm.local";
}
