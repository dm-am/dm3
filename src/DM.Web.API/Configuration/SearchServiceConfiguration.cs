namespace DM.Web.API.Configuration;

/// <summary>
/// Configuration for gRPC search service connection
/// </summary>
public class SearchServiceConfiguration
{
    /// <summary>
    /// gRPC endpoint address (e.g., "http://localhost:5001")
    /// </summary>
    public string GrpcEndpoint { get; set; } = string.Empty;
}