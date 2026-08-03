using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// The object store an upload's bytes live in: the single place that knows the
/// bucket, the caching a never-rewritten key allows and the address the object
/// is served from.
/// </summary>
/// <remarks>
/// The relational store has a context and the document store has a client; this
/// one had neither, so every caller held the vendor client and wrote its own
/// answer to "the key is already gone" — three callers, three different answers,
/// and the bucket name repeated at each of them. A second object per upload, a
/// different bucket or a move to batch deletes is one change here instead of
/// several that have to agree.
/// </remarks>
public interface IObjectStorage
{
    /// <summary>
    /// Store the bytes under the key. Keys are never reused, so nothing is
    /// overwritten and the object may be cached indefinitely.
    /// </summary>
    /// <param name="key">Object key.</param>
    /// <param name="content">Object bytes.</param>
    /// <param name="contentType">Validated MIME type of the content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
    /// Public address the object is served from.
    /// </summary>
    /// <param name="key">Object key.</param>
    string BuildPublicUrl(string key);
}
