using System.IO;

namespace DM.Web.API.Features.General.Upload;

/// <summary>
/// A file being handed to a caller who is entitled to it.
/// </summary>
/// <param name="Content">
/// The bytes, streamed straight from the object store. The response owns it and
/// disposes it.
/// </param>
/// <param name="ContentType">MIME type the file is stored as.</param>
/// <param name="Length">Size in bytes, or null when the store did not report one.</param>
/// <param name="FileName">
/// Display name, as sanitized when the file was accepted — never the raw name the
/// uploader sent.
/// </param>
/// <param name="OpenToGuest">
/// Whether a guest reads the file too, so the answer is the same for every caller
/// and a browser may keep it.
/// </param>
public sealed record UploadContent(
    Stream Content,
    string ContentType,
    long? Length,
    string FileName,
    bool OpenToGuest);
