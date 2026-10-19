using System;

namespace DM.Domain.Moderation.Features.Mentorships;

/// <summary>
/// A single mentorship assignment: a game or blog curated by a mentor
/// </summary>
public class MentorshipAssignment
{
    public Guid MentorId { get; set; }

    /// <summary>
    /// Curated target id (game or blog)
    /// </summary>
    public Guid TargetId { get; set; }

    public string Title { get; set; } = null!;
}
