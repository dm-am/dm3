using System.ComponentModel;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Website color theme
/// </summary>
public enum Theme
{
    /// <summary>
    /// Light theme
    /// </summary>
    [Description("Светлая")]
    Light = 0,

    /// <summary>
    /// Dark theme
    /// </summary>
    [Description("Темная")]
    Dark = 1
}
