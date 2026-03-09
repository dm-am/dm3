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
    /// Mongo connection string
    /// </summary>
    public string Mongo { get; set; } = null!;

    /// <summary>
    /// Mongo in-memory connection string
    /// </summary>
    public string Cache { get; set; } = null!;

    /// <summary>
    /// Search engine connection string
    /// </summary>
    public string SearchEngine { get; set; } = null!;

    /// <summary>
    /// Message queue connection string
    /// </summary>
    public string MessageQueue { get; set; } = null!;

    /// <summary>
    /// Logging storage connection string
    /// </summary>
    public string Logs { get; set; } = null!;

    /// <summary>
    /// Jaeger sink for Tracing
    /// </summary>
    public string TracingEndpoint { get; set; } = null!;
}