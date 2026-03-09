using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Entity DTO for creating a blog (repository level)
/// </summary>
public class CreateBlogEntity
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Owner user identifier
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Blog description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Draft visibility
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; }

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for updating a blog (repository level)
/// </summary>
public class UpdateBlogEntity
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// New title (if changed)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// New description (if changed)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// New visibility (if changed)
    /// </summary>
    public DraftVisibility? DraftVisibility { get; set; }

    /// <summary>
    /// Comments enabled flag (if changed)
    /// </summary>
    public bool? CommentsEnabled { get; set; }

    /// <summary>
    /// Update timestamp
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for creating a rubric (repository level)
/// </summary>
public class CreateRubricEntity
{
    /// <summary>
    /// Rubric identifier
    /// </summary>
    public Guid RubricId { get; set; }

    /// <summary>
    /// Parent blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Rubric title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Entity DTO for updating a rubric (repository level)
/// </summary>
public class UpdateRubricEntity
{
    /// <summary>
    /// Rubric identifier
    /// </summary>
    public Guid RubricId { get; set; }

    /// <summary>
    /// New title (if changed)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// New sort order (if changed)
    /// </summary>
    public int? SortOrder { get; set; }
}

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
}

/// <summary>
/// Entity DTO for adding a blog assistant (repository level)
/// </summary>
public class AddBlogAssistantEntity
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Join timestamp
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }
}
