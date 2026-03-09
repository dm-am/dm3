using System.Collections.Generic;

namespace DM.Infrastructure.Core.Configuration;

/// <summary>
/// Site mirror configuration
/// </summary>
public class MirrorConfiguration
{
    /// <summary>
    /// Current mirror ID (main, ru, etc.)
    /// </summary>
    public string CurrentMirrorId { get; set; } = "main";

    /// <summary>
    /// Available mirrors
    /// </summary>
    public Dictionary<string, MirrorInfo> Mirrors { get; set; } = new();

    /// <summary>
    /// Check if Russian mirror is enabled
    /// </summary>
    public bool IsRussianMirrorEnabled =>
        Mirrors.TryGetValue("ru", out var ru) && !string.IsNullOrEmpty(ru.WebUrl);
}

/// <summary>
/// Mirror information
/// </summary>
public class MirrorInfo
{
    /// <summary>
    /// Mirror ID
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Mirror name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Web URL (null = mirror not ready)
    /// </summary>
    public string? WebUrl { get; set; }

    /// <summary>
    /// API URL (null = mirror not ready)
    /// </summary>
    public string? ApiUrl { get; set; }
}
