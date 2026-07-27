using System;

namespace DM.Domain.Moderation.Features.Mentorships;

/// <summary>
/// A single mentorship assignment: a game or blog curated by a mentor
/// </summary>
public class MentorshipAssignment
{
    /// <summary>
    /// Curating mentor user id
    /// </summary>
    public Guid MentorId { get; set; }

    /// <summary>
    /// Curated target id (game or blog)
    /// </summary>
    public Guid TargetId { get; set; }

    /// <summary>
    /// Curated target title
    /// </summary>
    public string Title { get; set; } = null!;
}
