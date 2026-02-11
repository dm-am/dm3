using System;
using System.ComponentModel.DataAnnotations;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Community;

/// <summary>
/// Review information (platform, user, game, or post review)
/// </summary>
public class Review
{
    /// <summary>
    /// Review unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Review author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Review author details
    /// </summary>
    public UserSummary? Author { get; set; }

    /// <summary>
    /// Author login (for creating reviews on behalf of users, admin only)
    /// </summary>
    public string? AuthorLogin { get; set; }

    /// <summary>
    /// Type of entity being reviewed
    /// </summary>
    public ReviewTargetType TargetType { get; set; }

    /// <summary>
    /// Target entity identifier (null for Platform reviews)
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// Review text content
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Review sentiment (for Post reviews only)
    /// </summary>
    public ReviewSign? Sign { get; set; }

    /// <summary>
    /// Review reason type (for Post reviews only)
    /// </summary>
    public ReviewReasonType? ReasonType { get; set; }

    /// <summary>
    /// Target entity owner identifier (for Post reviews - post author)
    /// </summary>
    public Guid? TargetOwnerId { get; set; }

    /// <summary>
    /// Parent entity identifier (for Post reviews - GameId)
    /// </summary>
    public Guid? ParentEntityId { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last update timestamp (UTC)
    /// </summary>
    public DateTimeOffset? LastUpdateUtc { get; set; }

    /// <summary>
    /// Approval status (for Platform reviews with moderation)
    /// </summary>
    public bool? IsApproved { get; set; }
}

/// <summary>
/// Request to create a new review
/// </summary>
public class CreateReviewRequest
{
    /// <summary>
    /// Review text content (10-10000 characters for platform reviews)
    /// </summary>
    [StringLength(10000, MinimumLength = 10, ErrorMessage = "Review text must be between 10 and 10000 characters")]
    public string? Text { get; set; }

    /// <summary>
    /// Review sentiment (required for Post reviews)
    /// </summary>
    public ReviewSign? Sign { get; set; }

    /// <summary>
    /// Review reason type (optional for Post reviews)
    /// </summary>
    public ReviewReasonType? ReasonType { get; set; }
}

/// <summary>
/// Request to update an existing review
/// </summary>
public class UpdateReviewRequest
{
    /// <summary>
    /// Updated review text content
    /// </summary>
    [StringLength(10000, ErrorMessage = "Review text must not exceed 10000 characters")]
    public string? Text { get; set; }

    /// <summary>
    /// Updated review sentiment
    /// </summary>
    public ReviewSign? Sign { get; set; }

    /// <summary>
    /// Updated review reason type
    /// </summary>
    public ReviewReasonType? ReasonType { get; set; }

    /// <summary>
    /// Update approval status (admin only)
    /// </summary>
    public bool? IsApproved { get; set; }
}

/// <summary>
/// Query parameters for filtering reviews
/// </summary>
public class ReviewsQueryParams
{
    /// <summary>
    /// Filter by author login
    /// </summary>
    [StringLength(50, ErrorMessage = "Author login must not exceed 50 characters")]
    public string? AuthorLogin { get; set; }

    /// <summary>
    /// Filter by recipient login (for post reviews - post author)
    /// </summary>
    [StringLength(50, ErrorMessage = "Recipient login must not exceed 50 characters")]
    public string? RecipientLogin { get; set; }

    /// <summary>
    /// Filter by game ID (for post reviews)
    /// </summary>
    public Guid? GameId { get; set; }

    /// <summary>
    /// Filter by post ID (for post reviews)
    /// </summary>
    public Guid? PostId { get; set; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1")]
    public int Number { get; set; } = 1;

    /// <summary>
    /// Number of items per page (1-100)
    /// </summary>
    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
    public int Size { get; set; } = 20;
}
