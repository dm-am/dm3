using System;

namespace DM.Web.API.Features.General.Upload;

/// <summary>
/// The one address the bytes of an upload are served from.
/// </summary>
/// <remarks>
/// Spelled once: the upload record and every post attachment carry it. A path, not
/// an absolute URL: the client knows the origin, and a proxy may rewrite the host.
/// </remarks>
internal static class UploadContentRoute
{
    /// <summary>Address of the content endpoint for the given upload.</summary>
    /// <param name="uploadId">Upload identifier.</param>
    public static string For(Guid uploadId) => $"/v1/uploads/{uploadId:D}/content";
}
