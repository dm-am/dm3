namespace DM.Web.API.Shared.Http;

/// <summary>
/// Media types the API writes on the wire.
/// </summary>
/// <remarks>
/// One declaration because four places have to agree on the same two strings and
/// nothing but a comment used to hold them together: the error middleware writes
/// the refusal type, the rate limiter writes it on every 429, response
/// compression has to list it to compress refusals, and the Swagger filter has to
/// document it. A copy that drifts does not fail a build — it ships a response
/// the generated client refuses to parse.
/// </remarks>
internal static class ApiContentTypes
{
    /// <summary>What a successful response carries.</summary>
    public const string Json = "application/json";

    /// <summary>What every refusal carries (RFC 7807 problem details).</summary>
    public const string ProblemJson = "application/problem+json";
}
