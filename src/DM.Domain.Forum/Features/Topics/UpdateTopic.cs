using System;

namespace DM.Domain.Forum.Features.Topics;

/// <summary>
/// DTO model for updating existing topic
/// </summary>
public class UpdateTopic
{
    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// New title (null = don't update)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// New description (null = don't update)
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// New parent board title (null = don't update)
    /// </summary>
    public string? BoardTitle { get; set; }

    /// <summary>
    /// Is attached
    /// </summary>
    public bool? IsAttached { get; set; }

    /// <summary>
    /// Is closed
    /// </summary>
    public bool? IsClosed { get; set; }
}
