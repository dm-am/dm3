using System;

namespace DM.Domain.Blog.Features.Publications;

/// <summary>
/// Entity DTO for creating a publication (repository level)
/// </summary>
public class CreatePublicationEntity
{
    /// <summary>
    /// Publication identifier
    /// </summary>
    public Guid PublicationId { get; set; }

    /// <summary>
    /// Parent blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Rubric identifier (optional)
    /// </summary>
    public Guid? RubricId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Publication title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Publication content (HTML)
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Short preview/excerpt
    /// </summary>
    public string? Preview { get; set; }

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; }

    /// <summary>
    /// Whether to publish immediately
    /// </summary>
    public bool PublishImmediately { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for updating a publication (repository level)
/// </summary>
public class UpdatePublicationEntity
{
    /// <summary>
    /// Publication identifier
    /// </summary>
    public Guid PublicationId { get; set; }

    /// <summary>
    /// New rubric ID (if changed)
    /// </summary>
    public Guid? RubricId { get; set; }

    /// <summary>
    /// Whether to clear rubric
    /// </summary>
    public bool ClearRubric { get; set; }

    /// <summary>
    /// New title (if changed)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// New content (if changed)
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// New preview (if changed)
    /// </summary>
    public string? Preview { get; set; }

    /// <summary>
    /// Comments enabled flag (if changed)
    /// </summary>
    public bool? CommentsEnabled { get; set; }

    /// <summary>
    /// Published flag (if changed)
    /// </summary>
    public bool? IsPublished { get; set; }

    /// <summary>
    /// Update timestamp
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>
    /// Who is editing
    /// </summary>
    /// <remarks>
    /// Not always the author: an assistant and a moderator may edit somebody
    /// else's publication (PublicationIntentionResolver). The column existed and
    /// nothing wrote it, so the notification about a changed publication went out
    /// with no actor and could not be held against a reader's blacklist.
    /// </remarks>
    public Guid ModifiedByUserId { get; set; }
}
