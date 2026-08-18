using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.PostReviews;

/// <summary>
/// Input DTO for updating a post review
/// </summary>
public class UpdatePostReview
{
    /// <summary>
    /// Review ID to update
    /// </summary>
    public Guid ReviewId { get; set; }

    /// <summary>
    /// New review sign (null to keep current)
    /// </summary>
    public ReviewSign? Sign { get; set; }

    /// <summary>
    /// New review text, raw BBCode (null to keep current)
    /// </summary>
    /// <remarks>
    /// A review is a sign and the sentence that explains it, and the card shows
    /// the sentence. An update that could only move the sign left the author
    /// with a text they could not correct and a delete-and-write-again as the
    /// only way out — which the per-game cooldown then refuses for three days.
    /// </remarks>
    public string? Text { get; set; }
}
