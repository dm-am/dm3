using System;
using System.ComponentModel;
using System.Reflection;

namespace DM.Domain.Core.Extensions;

/// <summary>
/// Расширения получения описаний
/// </summary>
public static class DescriptionExtensions
{
    /// <summary>
    /// Получить описание <see cref="DescriptionAttribute" /> для значения значения перечисления
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string? GetDescription(this Enum value) =>
        value.GetType().GetField(value.ToString())
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description;
}
