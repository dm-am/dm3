namespace DM.Domain.Core.Configuration;

/// <summary>
/// Integration between DM sites configuration
/// </summary>
public class IntegrationSettings
{
    /// <summary>
    /// DM.API URL
    /// </summary>
    public string ApiUrl { get; set; } = null!;

    /// <summary>
    /// DM website URL
    /// </summary>
    public string WebUrl { get; set; } = null!;

    /// <summary>
    /// DM administration tools website URL
    /// </summary>
    public string AdminUrl { get; set; } = null!;

    /// <summary>
    /// DM mobile site URL
    /// </summary>
    public string MobileUrl { get; set; } = null!;

    /// <summary>
    /// External URLs for CORS policies
    /// </summary>
    public string[] CorsUrls { get; set; } = [];

    /// <summary>
    /// Base URL for static assets (logo, images in emails)
    /// </summary>
    public string StaticAssetsUrl { get; set; } = null!;
}
