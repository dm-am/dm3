using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Dto;

/// <summary>
/// Base interface for internal user DTOs in service layer.
/// </summary>
/// <remarks>
/// Implemented by GeneralUser and used for authorization checks,
/// identity management, and cross-service user data transfer.
/// </remarks>
public interface IUser
{
    /// <summary>
    /// Id
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// Display name (unique, changeable with approval)
    /// </summary>
    string Username { get; }

    /// <summary>
    /// Role
    /// </summary>
    UserRole Role { get; }

    /// <summary>
    /// Computed access restrictions
    /// </summary>
    AccessPolicy AccessPolicy { get; }

    /// <summary>
    /// Last time user performed any action on any site (UTC)
    /// </summary>
    DateTimeOffset? LastActivityUtc { get; }

    /// <summary>
    /// Rating participation flag
    /// </summary>
    bool RatingDisabled { get; set; }

    /// <summary>
    /// Sum of positive and negative reviews for user's posts
    /// </summary>
    int QualityRating { get; set; }

    /// <summary>
    /// Total number of user's posts
    /// </summary>
    int QuantityRating { get; set; }

    /// <summary>
    /// Whether the user is still on probation by post count
    /// </summary>
    bool IsNewbie { get; }

    /// <summary>
    /// Whether moderation is watching what this user creates: their new games and
    /// blogs are premoderated the way a newbie's are.
    /// </summary>
    /// <remarks>
    /// Set and cleared by hand by a moderator, and by nothing else. Deliberately
    /// not the same thing as the violators list, which is recomputed from active
    /// warnings and bans and therefore lapses on its own when they expire: this
    /// flag is the case where moderation decided the lapse should not happen yet.
    /// </remarks>
    bool IsUnderModerationWatch { get; set; }
}
