namespace DM.Domain.Core.Dto;

/// <summary>
/// Extensions for optional comparison
/// </summary>
public static class OptionalExtensions
{
    public static bool HasChanged<T>(this Optional<T> optional, T? againstValue) where T : struct
    {
        return optional != null && !optional.Value.Equals(againstValue);
    }
}
