using System;

namespace DM.Web.API.Features.General.Upload;

/// <summary>
/// The one address the bytes of an upload are served from.
/// </summary>
/// <remarks>
/// Spelled once, because two payloads carry it — the upload record itself and
/// every attachment on a post — and a second spelling is a second thing to keep
/// in step with the route attribute.
///
/// A path and not an absolute URL: the API is reached at one origin per
/// deployment and the client already knows which, while a host name composed on
/// the server would have to be configured and would be wrong behind the first
/// proxy that rewrites it.
/// </remarks>
internal static class UploadContentRoute
{
    /// <summary>Address of the content endpoint for the given upload.</summary>
    /// <param name="uploadId">Upload identifier.</param>
    public static string For(Guid uploadId) => $"/v1/uploads/{uploadId:D}/content";
}
