namespace DM.Domain.Core.Search;

/// <summary>
/// Search engine configuration
/// </summary>
public class SearchEngineConfiguration
{
    /// <summary>
    /// Searchable entity index name
    /// </summary>
    public const string IndexName = "dm_search";

    /// <summary>
    /// Connection string
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Basic auth username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Basic auth password
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
