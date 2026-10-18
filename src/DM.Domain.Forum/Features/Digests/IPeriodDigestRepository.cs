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
    /// <param name="topicId">Topic the digest will be written as.</param>
    /// <param name="year">Year of the period.</param>
    /// <param name="month">Month of the period, or null for a yearly digest.</param>
    /// <param name="recordedUtc">Moment the marker itself is created at.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>False when another instance had already claimed this period.</returns>
    Task<bool> TryClaimPeriod(
        Guid topicId,
        int year,
        int? month,
        DateTimeOffset recordedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the period back after a claim whose topic was never written.
    /// </summary>
    /// <remarks>
    /// The claim is a committed row of its own, taken before the topic exists, so
    /// a creation that refuses would otherwise leave the period marked as done
    /// with nothing to show for it: the marker names a topic that was never
    /// written, and every later pass reads it as "already generated". A monthly or
    /// yearly digest is generated once and never again, so that loss is permanent.
    /// Taking the marker back is conditional on the topic being absent, and that
    /// condition lives in the statement itself. Creation commits the topic before
    /// it reads the row back, and the read is outside the retry, so a refusal can
    /// reach the caller with the digest already posted; releasing the period there
    /// would put a second copy of the same digest on the board.
    /// </remarks>
    /// <param name="topicId">Topic the released claim was made under.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rows released: one when the period was given back, zero when the
    /// topic turned out to exist.</returns>
    Task<int> ReleasePeriod(Guid topicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Backdates the digest topic to the moment its period closed.
    /// </summary>
    /// <remarks>
    /// A digest lives at the moment its period closed, as if it had been posted
    /// right then, so a yearly digest caught up on in July sits in the news
    /// history instead of on top of it. The board's summary is recomputed after
    /// this by the topic repository, which owns the one definition of it.
    /// </remarks>
    /// <param name="topicId">Topic to backdate.</param>
    /// <param name="periodEndUtc">Moment the period closed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DateTopicAtPeriodEnd(
        Guid topicId, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default);
}
