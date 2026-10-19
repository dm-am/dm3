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
    /// Tells whether the digest of a period has already been generated; a null
    /// <paramref name="month" /> is the yearly digest.
    /// </summary>
    Task<bool> DigestExists(int year, int? month, CancellationToken ct = default);

    /// <summary>
    /// Takes the period for the given topic, unless another instance has taken it
    /// already.
    /// </summary>
    /// <remarks>
    /// Claimed before the topic is created, and the topic is only created by the
    /// caller that won: a claim made afterwards left the loser holding a topic it
    /// had to take back, and taking it back is refused by the board's own
    /// reference to its last topic.
    ///
    /// The marker is what makes the digest idempotent, so the identifier it names
    /// is minted by the caller and the topic is written under it.
    /// </remarks>
    /// <returns>False when another instance had already claimed this period.</returns>
    Task<bool> TryClaimPeriod(
        Guid topicId,
        int year,
        int? month,
        DateTimeOffset recordedUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Gives the period back after a claim whose topic was never written.
    /// </summary>
    /// <remarks>
    /// A digest is generated once and never again, so a claim left behind by a
    /// creation that refused loses it for good. The release is conditional, in the
    /// statement itself, on the topic being absent: creation can refuse after
    /// committing the topic, and releasing then would post the digest twice.
    /// </remarks>
    /// <returns>Rows released: one when the period was given back, zero when the
    /// topic turned out to exist.</returns>
    Task<int> ReleasePeriod(Guid topicId, CancellationToken ct = default);

    /// <summary>
    /// Backdates the digest topic to the moment its period closed; the board's
    /// summary is recomputed after this by the topic repository, which owns it.
    /// </summary>
    Task DateTopicAtPeriodEnd(
        Guid topicId, DateTimeOffset periodEndUtc, CancellationToken ct = default);
}
