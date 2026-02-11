using System;

namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Role restrictions to perform some action with boards
/// </summary>
[Flags]
public enum BoardAccessPolicy
{
    /// <summary>
    /// No one is allowed
    /// </summary>
    None = 0,

    /// <summary>
    /// Administrators allowed
    /// </summary>
    Administrator = 1 << 0,

    /// <summary>
    /// Senior moderators allowed
    /// </summary>
    SeniorModerator = 1 << 1,

    /// <summary>
    /// Moderators allowed
    /// </summary>
    Moderator = 1 << 2,

    /// <summary>
    /// Mentors allowed
    /// </summary>
    Mentor = 1 << 3,

    /// <summary>
    /// Any authenticated user allowed
    /// </summary>
    RegularUser = 1 << 5,

    /// <summary>
    /// Anyone allowed
    /// </summary>
    Guest = 1 << 6
}
