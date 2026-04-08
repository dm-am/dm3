using System;

namespace DM.Web.API.Features.Moderation.Tags;

/// <summary>
/// Tag group for management
/// </summary>
public class TagGroup
{
    /// <summary>
    /// Tag group ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order (lower values appear first)
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Number of tags in this group
    /// </summary>
    public int TagsCount { get; set; }
}

/// <summary>
/// Tag for management
/// </summary>
public class Tag
{
    /// <summary>
    /// Tag ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Short numeric ID for URLs
    /// </summary>
    public int ShortId { get; set; }

    /// <summary>
    /// Tag group ID
    /// </summary>
    public Guid GroupId { get; set; }

    /// <summary>
    /// Tag group title
    /// </summary>
    public string GroupTitle { get; set; } = string.Empty;

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order within group
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Number of games using this tag
    /// </summary>
    public int GamesCount { get; set; }
}

/// <summary>
/// Create tag group request
/// </summary>
public class CreateTagGroupRequest
{
    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Update tag group request
/// </summary>
public class UpdateTagGroupRequest
{
    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Create tag request
/// </summary>
public class CreateTagRequest
{
    /// <summary>
    /// Tag group ID
    /// </summary>
    public Guid GroupId { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order within group
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Update tag request
/// </summary>
public class UpdateTagRequest
{
    /// <summary>
    /// Tag group ID
    /// </summary>
    public Guid GroupId { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order within group
    /// </summary>
    public int SortOrder { get; set; }
}
