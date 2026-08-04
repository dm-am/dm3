using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Forum.Features.Digests;

/// <summary>
/// Storage side of the period digest topics.
/// </summary>
public interface IPeriodDigestRepository
{
    /// <summary>
    /// Tells whether the digest of a period has already been generated.
    /// </summary>
    /// <param name="year">Year of the period.</param>
    /// <param name="month">Month of the period, or null for a yearly digest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> DigestExists(int year, int? month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Backdates the topic to the moment its period closed and records the marker
    /// that makes the digest idempotent, in one commit.
    /// </summary>
    /// <remarks>
    /// The two writes are one commit on purpose: a marker that did not land would
    /// let the next pass generate the same digest again. When the commit is
    /// refused — the unique index catching another instance that got there first —
    /// the topic just created is removed, so no half-made digest survives.
    /// </remarks>
    /// <param name="topicId">Topic that was created for the digest.</param>
    /// <param name="year">Year of the period.</param>
    /// <param name="month">Month of the period, or null for a yearly digest.</param>
    /// <param name="periodEndUtc">Moment the period closed; the topic is dated to it.</param>
    /// <param name="recordedUtc">Moment the marker itself is created at.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>False when another instance had already generated this digest.</returns>
    Task<bool> TryRecordDigest(
        Guid topicId,
        int year,
        int? month,
        DateTimeOffset periodEndUtc,
        DateTimeOffset recordedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes a board's denormalized "last topic" fields from its actual
    /// freshest topic.
    /// </summary>
    /// <remarks>
    /// Creating the digest stamps them with it, and backdating the digest makes
    /// that stamp wrong: the board would advertise a topic dated months ago as
    /// its newest.
    /// </remarks>
    /// <param name="boardId">Board to recompute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshLastTopic(Guid boardId, CancellationToken cancellationToken = default);
}
