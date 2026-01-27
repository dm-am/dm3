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
    NoOne = 0,

    /// <summary>
    /// Administrators allowed
    /// </summary>
    Administrator = 1 << 0,

    /// <summary>
    /// Senior moderators allowed
    /// </summary>
    SeniorModerator = 1 << 1,

    /// <summary>
    /// Regular moderators allowed
    /// </summary>
    RegularModerator = 1 << 2,

    /// <summary>
    /// Mentor moderators allowed
    /// </summary>
    MentorModerator = 1 << 3,

    /// <summary>
    /// Board moderators allowed
    /// </summary>
    BoardModerator = 1 << 4,

    /// <summary>
    /// Any authenticated user allowed
    /// </summary>
    Player = 1 << 5,

    /// <summary>
    /// Anyone allowed
    /// </summary>
    Guest = 1 << 6
}
