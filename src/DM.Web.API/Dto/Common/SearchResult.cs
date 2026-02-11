using System;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Common;

/// <summary>
/// Unified search results
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Matching users
    /// </summary>
    public UserSummary[] Users { get; set; } = Array.Empty<UserSummary>();

    /// <summary>
    /// Matching games
    /// </summary>
    public GameSummary[] Games { get; set; } = Array.Empty<GameSummary>();

    /// <summary>
    /// Matching posts
    /// </summary>
    public PostSummary[] Posts { get; set; } = Array.Empty<PostSummary>();

    /// <summary>
    /// Pagination information
    /// </summary>
    public Paging? Paging { get; set; }
}

/// <summary>
/// Game summary for search results
/// </summary>
public class GameSummary
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Game master login
    /// </summary>
    public string MasterLogin { get; set; } = string.Empty;

    /// <summary>
    /// Game system name (e.g., DnD 5e, Pathfinder)
    /// </summary>
    public string? SystemName { get; set; }

    /// <summary>
    /// Current game status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Total posts count
    /// </summary>
    public int PostsCount { get; set; }
}

/// <summary>
/// Post summary for search results
/// </summary>
public class PostSummary
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = string.Empty;

    /// <summary>
    /// Room title
    /// </summary>
    public string RoomTitle { get; set; } = string.Empty;

    /// <summary>
    /// Author login
    /// </summary>
    public string AuthorLogin { get; set; } = string.Empty;

    /// <summary>
    /// Post text preview (truncated)
    /// </summary>
    public string TextPreview { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Search query parameters
/// </summary>
public class SearchQuery
{
    /// <summary>
    /// Search query string
    /// </summary>
    public string Q { get; set; } = string.Empty;

    /// <summary>
    /// Types to search (comma-separated: users,games,posts)
    /// </summary>
    public string? Types { get; set; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Number { get; set; } = 1;

    /// <summary>
    /// Page size
    /// </summary>
    public int Size { get; set; } = 20;
}
