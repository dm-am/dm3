using System;

namespace DM.Services.Forum.Dto.Input;

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
    /// New title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// New description
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// New parent board title
    /// </summary>
    public string BoardTitle { get; set; } = null!;

    /// <summary>
    /// Is attached
    /// </summary>
    public bool? IsAttached { get; set; }

    /// <summary>
    /// Is closed
    /// </summary>
    public bool? IsClosed { get; set; }
}