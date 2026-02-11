using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Reviews.Creating;

/// <summary>
/// DTO model for review creating
/// </summary>
public class CreateReview
{
    /// <summary>
    /// Review text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Author login (only for admin-created reviews)
    /// </summary>
    public string? AuthorLogin { get; set; }

    /// <summary>
    /// Type of entity being reviewed (Platform, User, Game)
    /// </summary>
    public ReviewTargetType TargetType { get; set; } = ReviewTargetType.Platform;

    /// <summary>
    /// Target entity identifier (UserId for User reviews, GameId for Game reviews)
    /// </summary>
    public Guid? TargetId { get; set; }
}