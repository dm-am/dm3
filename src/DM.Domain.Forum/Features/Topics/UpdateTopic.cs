using System;

namespace DM.Domain.Forum.Features.Topics;

/// <summary>
/// A null field is left as it is.
/// </summary>
public class UpdateTopic
{
    public Guid TopicId { get; set; }

    public string? Title { get; set; }

    public string? Text { get; set; }

    /// <summary>
    /// New parent board title
    /// </summary>
    public string? BoardTitle { get; set; }

    public bool? IsAttached { get; set; }

    public bool? IsClosed { get; set; }
}
