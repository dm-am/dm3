using System.ComponentModel;

namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Website color schema
/// </summary>
public enum ColorSchema
{
    /// <summary>
    /// Light color scheme
    /// </summary>
    [Description("Светлая")]
    Light = 0,

    /// <summary>
    /// Dark color scheme
    /// </summary>
    [Description("Тёмная")]
    Dark = 1
}
