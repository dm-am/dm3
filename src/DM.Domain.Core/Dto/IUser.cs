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
}
