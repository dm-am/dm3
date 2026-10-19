namespace DM.Web.API.Shared.Http;

/// <summary>
/// Media types the API writes on the wire.
/// </summary>
/// <remarks>
/// One declaration: the error middleware, the rate limiter, response compression
/// and the Swagger filter have to agree on the same two strings.
/// </remarks>
internal static class ApiContentTypes
{
    /// <summary>What a successful response carries.</summary>
    public const string Json = "application/json";

    /// <summary>What every refusal carries (RFC 7807 problem details).</summary>
    public const string ProblemJson = "application/problem+json";
}
