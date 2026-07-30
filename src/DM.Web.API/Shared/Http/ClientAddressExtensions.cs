using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Shared.Http;

/// <summary>
/// Client network address access
/// </summary>
public static class ClientAddressExtensions
{
    private const string UnknownAddress = "unknown";

    /// <summary>
    /// Network address of the client, as established by the forwarded headers
    /// middleware. Never read X-Forwarded-For directly: the header is written by
    /// the caller and only the middleware knows which of its entries came from a
    /// trusted proxy.
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Client address, or "unknown" for a connection without a peer address</returns>
    public static string GetClientAddress(this HttpContext httpContext)
    {
        var address = httpContext.Connection.RemoteIpAddress;
        if (address == null)
        {
            return UnknownAddress;
        }

        // A dual-stack socket reports IPv4 peers as ::ffff:1.2.3.4 while a forwarded
        // header carries plain 1.2.3.4. Recorded as two spellings, the same client
        // looks like two clients to the login journal and to alt-account correlation.
        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4().ToString()
            : address.ToString();
    }
}
