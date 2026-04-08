using System;
using System.ComponentModel;
using System.Reflection;

namespace DM.Domain.Core.Extensions;

/// <summary>
/// Description retrieval extensions
/// </summary>
public static class DescriptionExtensions
{
    /// <summary>
    /// Get <see cref="DescriptionAttribute" /> value for an enum value
    /// </summary>
    /// <param name="value">Enum value</param>
    /// <returns>Description attribute value or null if not found</returns>
    public static string? GetDescription(this Enum value) =>
        value.GetType().GetField(value.ToString())
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description;
}
