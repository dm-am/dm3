using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Reviews;

/// <summary>
/// Shared DTO model for review (platform, user, game, post reviews)
/// </summary>
public class Review
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Review author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Type of entity being reviewed
    /// </summary>
    public ReviewTargetType TargetType { get; set; }

    /// <summary>
    /// Target entity identifier (UserId for User reviews, GameId for Game reviews, PostId for Post reviews, null for Platform)
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// Target user details (for User reviews)
    /// </summary>
    public GeneralUser? TargetUser { get; set; }

    /// <summary>
    /// Target game title (for Game reviews)
    /// </summary>
    public string? TargetGameTitle { get; set; }

    /// <summary>
    /// Creating moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Review is published (only for Platform reviews with moderation)
    /// </summary>
    public bool Approved { get; set; }

    /// <summary>
    /// Review text (optional for Post reviews)
    /// </summary>
    public string? Text { get; set; }

    #region Post Review Fields

    /// <summary>
    /// Review sentiment sign (only for Post reviews)
    /// </summary>
    public ReviewSign? Sign { get; set; }

    /// <summary>
    /// Review reason type (only for Post reviews)
    /// </summary>
    public ReviewReasonType? ReasonType { get; set; }

    /// <summary>
    /// Post author identifier (only for Post reviews)
    /// </summary>
    public Guid? PostAuthorId { get; set; }

    /// <summary>
    /// Post author (only for Post reviews)
    /// </summary>
    public GeneralUser? PostAuthor { get; set; }

    /// <summary>
    /// Game identifier (only for Post reviews)
    /// </summary>
    public Guid? GameId { get; set; }

    #endregion
}
