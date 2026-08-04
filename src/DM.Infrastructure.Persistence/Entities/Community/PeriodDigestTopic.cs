using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// Marker linking a closed statistics period (calendar month or year) to its
/// auto-created "Итоги …" forum topic. One row per generated digest — the
/// idempotency record for the digest hosted service (a period whose row
/// exists is never generated again, even if the topic was later renamed) and
/// the future lookup for surfacing the digest topic on the home page.
/// A null <see cref="Month"/> means a yearly digest.
/// </summary>
[Table("PeriodDigestTopics")]
public class PeriodDigestTopic
{
    [Key]
    public Guid PeriodDigestTopicId { get; set; }

    public int Year { get; set; }

    public int? Month { get; set; }

    /// <summary>
    /// The digest topic. Loose reference (no navigation/FK) — same idiom as
    /// the other marker tables (PostEdits): consumers join explicitly.
    /// </summary>
    public Guid TopicId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}
