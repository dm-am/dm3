namespace DM.Infrastructure.Core.Configuration;

/// <summary>
/// Storage connection configuration
/// </summary>
public class ConnectionStrings
{
    /// <summary>
    /// RDB connection string
    /// </summary>
    public string Rdb { get; set; } = null!;

    /// <summary>
    /// Logging storage connection string
    /// </summary>
    public string Logs { get; set; } = null!;

    /// <summary>
    /// Jaeger sink for Tracing
    /// </summary>
    public string TracingEndpoint { get; set; } = null!;
}
