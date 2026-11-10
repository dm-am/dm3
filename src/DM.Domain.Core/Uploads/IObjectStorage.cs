namespace DM.Domain.Core.Uploads;

/// <summary>
/// The object store an upload's bytes live in: the single place that knows the
/// bucket, the caching a never-rewritten key allows and the address the object
/// is served from.
/// </summary>
public interface IObjectStorage
{
    /// <summary>
    /// Store the bytes under the key. Keys are never reused, so nothing is
    /// overwritten and the object may be cached indefinitely.
    /// </summary>
    /// <param name="contentType">Validated MIME type of the content.</param>
    Task PutAsync(string key, byte[] content, string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove the object. Idempotent: a key that is not there counts as removed,
    /// which is what a retried delete meets.
    /// </summary>
    /// <param name="key">Object key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// True once the key holds nothing, false when the store refused and the
    /// caller may try again later. The refusal is logged here, so a caller with
    /// nowhere to retry is free to ignore the answer.
    /// </returns>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Open the object's bytes for reading, or null when the key holds nothing.
    /// </summary>
    /// <remarks>
    /// The closed prefixes have no public address by design, so the only way their
    /// bytes reach a browser is an endpoint that authorizes the caller and then
    /// streams them. Reading was missing from this interface for exactly as long
    /// as no such endpoint existed.
    ///
    /// The stream is the caller's to dispose, and it is a live network stream:
    /// nothing here buffers the object, so a file of any allowed size costs one
    /// connection rather than its own size in memory.
    /// </remarks>
    Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Public address the object is served from.
    /// </summary>
    string BuildPublicUrl(string key);
}

/// <summary>
/// An object's bytes together with what the store says about them.
/// </summary>
/// <param name="Content">
/// The bytes, as a stream the caller disposes. Owned by the caller from the
/// moment it is handed over, including when the response it feeds is abandoned.
/// </param>
/// <param name="ContentType">MIME type recorded on the object when it was written.</param>
/// <param name="Length">
/// Size in bytes, or null when the store did not report one — a response may then
/// only be sent without Content-Length, never with a guessed one.
/// </param>
public sealed record StoredObject(Stream Content, string ContentType, long? Length);
