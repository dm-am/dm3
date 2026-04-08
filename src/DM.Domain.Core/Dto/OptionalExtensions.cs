namespace DM.Domain.Core.Dto;

/// <summary>
/// Extensions for optional comparison
/// </summary>
public static class OptionalExtensions
{
    /// <summary>
    /// Checks if the optional field is expected to change
    /// </summary>
    /// <param name="optional">Optional value to check</param>
    /// <param name="againstValue">Value to compare against</param>
    /// <typeparam name="T">Value type</typeparam>
    /// <returns>True if the optional has a different value</returns>
    public static bool HasChanged<T>(this Optional<T> optional, T? againstValue) where T : struct
    {
        return optional != null && !optional.Value.Equals(againstValue);
    }
}
